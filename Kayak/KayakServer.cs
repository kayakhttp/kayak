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

        public IPEndPoint ListenEndPoint { get; private set; }
        IScheduler scheduler;

        volatile int connections;
        volatile int listening;
        volatile int closed;
        volatile int disposed;

        Socket listener;

        public KayakServer(IScheduler scheduler)
        {
            this.scheduler = scheduler;
        }

        public void Dispose()
        {
            if (Interlocked.CompareExchange(ref disposed, 1, 0) == 1)
                throw new ObjectDisposedException("socket");

            if (listener != null)
            {
                listener.Dispose();
                listener = null;
            }
        }

        public void Listen(IPEndPoint ep)
        {
            if (Interlocked.CompareExchange(ref disposed, 1, 0) == 1)
                throw new ObjectDisposedException("socket");

            if (Interlocked.CompareExchange(ref listening, 1, 0) == 1)
                throw new InvalidOperationException("Already listening.");

            ListenEndPoint = ep;

            Debug.WriteLine("KayakServer binding to " + ListenEndPoint);

            listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.IP);
            listener.Bind(ListenEndPoint);
            listener.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReceiveTimeout, 10000);
            listener.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.SendTimeout, 10000);
            listener.Listen(1000);

            AcceptNext();
        }

        public void Close()
        {
            if (Interlocked.CompareExchange(ref disposed, 1, 0) == 1)
                throw new ObjectDisposedException("socket");

            if (Interlocked.CompareExchange(ref listening, 0, 1) == 0)
                throw new InvalidOperationException("Not listening.");

            if (Interlocked.CompareExchange(ref closed, 1, 0) == 1)
                throw new InvalidOperationException("Already closed.");

            ListenEndPoint = null;

            // XXX should this happen on the worker thread?
            Console.WriteLine("Closing listening socket.");
            listener.Close();
            Dispose();

            RaiseOnClosedIfNecessary();
        }

        internal void SocketClosed(KayakSocket socket)
        {
            Debug.WriteLine("Connection " + socket.id + ": closed (" + connections + " active connections)");
            Interlocked.Decrement(ref connections);
            RaiseOnClosedIfNecessary();
        }

        void RaiseOnClosedIfNecessary()
        {
            Debug.WriteLine(connections + " active connections.");

            if (closed == 1 && connections == 0)
            {
                if (OnClose != null)
                    OnClose(this, EventArgs.Empty);
            }
        }

        void AcceptNext()
        {
            try
            {
                Debug.WriteLine("BeginAccept");
                listener.BeginAccept(iasr =>
                {
                    Socket socket = null;
                    if (listener == null) return;
                    try
                    {
                        Debug.WriteLine("EndAccept");
                        socket = listener.EndAccept(iasr);
                        AcceptNext();
                    }
                    catch (ObjectDisposedException e)
                    {
                        return;
                    }

                    scheduler.Post(() =>
                        {
                            try
                            {
                                Interlocked.Increment(ref connections);
                                var s = new KayakSocket(socket, this, this.scheduler);
                                Debug.WriteLine("Connection " + s.id + ": accepted (" + connections + " active connections)");
                                if (OnConnection != null)
                                {
                                    OnConnection(this, new ConnectionEventArgs(s));
                                    s.DoRead(); // TODO defer til OnData event gets listener?
                                }
                                else
                                {
                                    s.End();
                                    s.Dispose();
                                }
                            }
                            catch (Exception e)
                            {
                                Interlocked.Decrement(ref connections);
                                Debug.WriteLine("Error while accepting connection.");
                                e.PrintStacktrace();
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
                listener.Close();
                Debug.WriteLine("Error attempting to accept next connection.");
                e.PrintStacktrace();

                if (OnClose != null)
                    OnClose(this, EventArgs.Empty);
            }
        }
    }
}
