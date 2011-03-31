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

        int connections;
        Socket listener;
        bool listening;
        bool closed;

        public KayakServer(IScheduler scheduler)
        {
            this.scheduler = scheduler;
        }

        public void Dispose()
        {
            if (listener != null)
            {
                listener.Dispose();
                listener = null;
            }
        }

        public void Listen(IPEndPoint ep)
        {
            if (listening)
                throw new InvalidOperationException("Already listening.");

            ListenEndPoint = ep;

            Debug.WriteLine("KayakServer binding to " + ListenEndPoint);
            listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.IP);
            listener.Bind(ListenEndPoint);
            listener.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReceiveTimeout, 10000);
            listener.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.SendTimeout, 10000);
            listener.Listen(1000);

            AcceptNext();
            listening = true;
            closed = false;
        }

        public void Close()
        {
            Console.WriteLine("Closing???");
            if (!listening)
                throw new InvalidOperationException("Not listening.");

            if (closed)
                throw new InvalidOperationException("Already closed.");

            listening = false;
            closed = true;
            ListenEndPoint = null;

            // XXX should this happen on the worker thread?
            Console.WriteLine("Closing listening socket.");
            listener.Close();
            Dispose();

            scheduler.Post(() =>
            {
                Debug.WriteLine("Closed listener.");
                RaiseOnClosedIfNecessary();
            });
        }

        internal void SocketClosed(KayakSocket socket)
        {
            Debug.WriteLine("Connection " + socket.id + ": closed (" + connections + " active connections)");
            connections--;
            RaiseOnClosedIfNecessary();
        }

        void RaiseOnClosedIfNecessary()
        {
            Debug.WriteLine(connections + " active connections.");

            if (closed && connections == 0)
            {
                closed = false;

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
                                connections++;
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
                                connections--;
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
