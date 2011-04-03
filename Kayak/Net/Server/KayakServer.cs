using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace Kayak
{
    public class KayakServer : IServer
    {
        public event EventHandler<ConnectionEventArgs> OnConnection;
        public event EventHandler OnClose;

        public IPEndPoint ListenEndPoint { get { return (IPEndPoint)listener.LocalEndPoint; } }

        IScheduler scheduler;
        KayakServerState state;
        Socket listener;

        public KayakServer(IScheduler scheduler)
        {
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

        public void Listen(IPEndPoint ep)
        {
            state.SetListening();

            Debug.WriteLine("KayakServer binding to " + ListenEndPoint);

            listener.Bind(ep);
            listener.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReceiveTimeout, 10000);
            listener.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.SendTimeout, 10000);
            listener.Listen(1000);

            AcceptNext();
        }

        public void Close()
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
            if (OnClose != null)
                OnClose(this, EventArgs.Empty);
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
                        if (OnConnection != null)
                        {
                            state.IncrementConnections();
                            OnConnection(this, new ConnectionEventArgs(s));
                            s.DoRead(); // TODO defer til OnData event gets listener
                        }
                        else
                        {
                            s.End();
                            s.Dispose();
                        }
                    });

                }, null);
            }
            catch (ObjectDisposedException e)
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
            e.PrintStacktrace();

            if (OnClose != null)
                OnClose(this, EventArgs.Empty);
        }
    }
}
