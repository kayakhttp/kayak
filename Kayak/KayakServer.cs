using System;
using System.IO;
using System.Net;
using System.Disposables;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Linq;

namespace Kayak
{
    public interface ISocket : IDisposable
    {
        IPEndPoint RemoteEndPoint { get; }
        Stream GetStream();
    }

    public class KayakServer : IObservable<ISocket>
    {
        public IPEndPoint ListenEndPoint { get; private set; }

        object gate = new object();
        
        Socket listener;
        Func<IObservable<Socket>> accept;
        int backlog;
        IObserver<ISocket> observer;
        IDisposable acceptSubscription;
        bool unsubscribed;

        int activeSockets;

        public KayakServer() : this(new IPEndPoint(IPAddress.Any, 8080)) { }
        public KayakServer(IPEndPoint listenEndPoint) : this(listenEndPoint, 1000) { }
        public KayakServer(IPEndPoint listenEndPoint, int backlog)
        {
            ListenEndPoint = listenEndPoint;
            this.backlog = backlog;
        }

        public IDisposable Subscribe(IObserver<ISocket> observer)
        {
            if (this.observer != null)
                throw new InvalidOperationException("Listener already has observer.");

            this.observer = observer;

            listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.IP);
            listener.Bind(ListenEndPoint);
            listener.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReceiveTimeout, 10000);
            listener.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.SendTimeout, 10000);
            listener.Listen(backlog);

            accept = Observable.FromAsyncPattern<Socket>(listener.BeginAccept, listener.EndAccept);

            AcceptNext();
            return Disposable.Create(() => 
            {
                lock (gate)
                {
                    unsubscribed = true;
                    listener.Close(); 
                }
            });
        }

        void AcceptNext()
        {
            if (acceptSubscription != null)
                acceptSubscription.Dispose();

            acceptSubscription = accept().Subscribe(socket =>
                {
                    var s = new DotNetSocket(this, socket);

                    lock (gate)
                        activeSockets++;

                    observer.OnNext(s);
                },
                e =>
                {
                    observer.OnError(e);
                },
                () =>
                {
                    bool isUnsubscribed = false;
                    
                    lock (gate)
                        isUnsubscribed = unsubscribed;

                    if (!isUnsubscribed)
                        AcceptNext();
                });
        }

        void SocketClosed()
        {
            bool complete = false;

            lock (gate)
                complete = --activeSockets == 0 && unsubscribed;

            if (complete)
            {
                observer.OnCompleted();
                observer = null;
            }
        }

        class DotNetSocket : ISocket
        {
            KayakServer server;
            Socket socket;
            NetworkStream stream;

            public DotNetSocket(KayakServer server, Socket socket)
            {
                this.server = server;
                this.socket = socket;
            }

            public IPEndPoint RemoteEndPoint
            {
                get { return (IPEndPoint)socket.RemoteEndPoint; }
            }

            public Stream GetStream()
            {
                if (stream == null)
                    stream = new NetworkStream(socket, true);

                return stream;
            }

            public void Dispose()
            {
                socket.Close();
                server.SocketClosed();
            }
        }
    }
}
