using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace Kayak
{
    public class KayakServer : IServer
    {
        IServerDelegate del;

        public IPEndPoint ListenEndPoint { get { return (IPEndPoint)listener.LocalEndPoint; } }

        IScheduler scheduler;
        KayakServerState state;
        Socket listener;

        class KayakServerFactory : IServerFactory
        {
            public IServer Create(IServerDelegate del, IScheduler scheduler)
            {
                return new KayakServer(del, scheduler);
            }
        }

        readonly static KayakServerFactory factory = new KayakServerFactory();

        public static IServerFactory Factory { get { return factory; } }

        internal KayakServer(IServerDelegate del, IScheduler scheduler)
        {
            if (del == null)
                throw new ArgumentNullException("del");

            if (scheduler == null)
                throw new ArgumentNullException("scheduler");

            this.del = del;
            this.scheduler = scheduler;
            listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.IP);
            state = new KayakServerState();
        }

        public void Dispose()
        {
            state.SetDisposed();

            if (listener != null)
            {
                listener.Dispose();
            }
        }

        public IDisposable Listen(IPEndPoint ep)
        {
            state.SetListening();

            Debug.WriteLine("KayakServer binding to " + ListenEndPoint);

            listener.Bind(ep);
            listener.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReceiveTimeout, 10000);
            listener.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.SendTimeout, 10000);
            listener.Listen(1000);

            AcceptNext();
            return new Disposable(() => Close());
        }

        void Close()
        {
            var closed = state.SetClosing();
            
            Console.WriteLine("Closing listening socket.");
            listener.Close();

            if (closed)
                RaiseOnClose();
        }

        internal void SocketClosed(KayakSocket socket)
        {
            //Debug.WriteLine("Connection " + socket.id + ": closed (" + connections + " active connections)");
            if (state.DecrementConnections())
                RaiseOnClose();
        }

        void RaiseOnClose()
        {
            del.OnClose(this);
        }

        void AcceptNext()
        {
            try
            {
                Debug.WriteLine("KayakServer: accepting connection");
                listener.BeginAccept(iasr =>
                {
                    Socket socket = null;
                    Exception error = null;
                    try
                    {
                        socket = listener.EndAccept(iasr);
                        AcceptNext();
                    }
                    catch (Exception e)
                    {
                        error = e;
                    }

                    if (error is ObjectDisposedException)
                        return;

                    scheduler.Post(() =>
                    {
                        Debug.WriteLine("KayakServer: accepted connection");
                        if (error != null)
                            HandleAcceptError(error);

                        var s = new KayakSocket(socket, this.scheduler);
                        state.IncrementConnections();

                        var socketDelegate = del.OnConnection(this, s);
                        s.del = socketDelegate;
                        s.BeginRead();
                    });

                }, null);
            }
            catch (ObjectDisposedException)
            {
                return;
            }
            catch (Exception e)
            {
                HandleAcceptError(e);
            }
        }

        void HandleAcceptError(Exception e)
        {
            state.SetError();

            try
            {
                listener.Close();
            }
            catch { }

            Debug.WriteLine("Error attempting to accept connection.");
            Console.Error.WriteStacktrace(e);

            RaiseOnClose();
        }
    }
}
