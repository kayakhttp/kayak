using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;

namespace Kayak
{
    public class KayakServer : IServer
    {
        public short Backlog;
        public IServerDelegate Delegate { get; set; }
        public IPEndPoint ListenEndPoint { get; private set; }
        public KayakScheduler Scheduler { get; private set; }

        int connections;
        bool closed;
        Socket listener;

        public KayakServer()
        {
        }

        public void Listen(IPEndPoint ep)
        {
            ListenEndPoint = ep;

            listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.IP);
            listener.Bind(ListenEndPoint);
            listener.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReceiveTimeout, 10000);
            listener.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.SendTimeout, 10000);
            listener.Listen(Backlog);

            Scheduler = new KayakScheduler();
            Scheduler.Post(AcceptNext);
            Scheduler.Start();
        }

        public void Close()
        {
            Scheduler.Post(() =>
            {
                listener.Close();
                Debug.WriteLine("Closed listener.");
                StopIfNoConnections();
            });
        }

        internal void SocketClosed()
        {
            connections--;
            Debug.WriteLine("Socket closed");
            if (closed)
                StopIfNoConnections();
        }

        void StopIfNoConnections()
        {
            Debug.WriteLine(connections + " active connections.");
            if (connections == 0)
            {
                Scheduler.Stop();
                Delegate.OnClose(this);
            }
        }

        void AcceptNext()
        {
            try
            {
                listener.BeginAccept(iasr =>
                {
                    Scheduler.Post(() =>
                        {
                            try
                            {
                                connections++;
                                Delegate.OnConnection(this, new KayakSocket(listener.EndAccept(iasr), this));
                            }
                            catch (ObjectDisposedException e)
                            {
                                return;
                            }
                            catch (Exception e)
                            {
                                connections--;
                                Debug.WriteLine("Error while accepting connection.");
                                e.PrintStacktrace();
                            }
                            AcceptNext();
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
                Delegate.OnClose(this);
            }
        }
    }
}
