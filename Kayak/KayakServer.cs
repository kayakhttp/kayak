using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Threading;

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
            Scheduler.Start();
            Scheduler.Post(AcceptNext);
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

        internal void SocketClosed(KayakSocket socket)
        {
            connections--;
            //Console.WriteLine("Connection " + socket.id + ": closed (" + connections + " active connections)");
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
                    Socket socket = null;

                    try
                    {
                        socket = listener.EndAccept(iasr);
                        AcceptNext();
                    }
                    catch (ObjectDisposedException e)
                    {
                        return;
                    }

                    Scheduler.Post(() =>
                        {
                            try
                            {
                                connections++;
                                var s = new KayakSocket(socket, this);
                                //Console.WriteLine("Connection " + s.id + ": accepted (" + connections + " active connections)");
                                Delegate.OnConnection(this, s);
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
                Delegate.OnClose(this);
            }
        }
    }
}
