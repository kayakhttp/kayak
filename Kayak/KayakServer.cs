using System;
using System.Collections.Generic;
using System.Disposables;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Kayak.Core;

namespace Kayak
{
    /// <summary>
    /// KayakServer is a simple server implementation using `System.Net.Sockets.Socket`. It is an 
    /// observable which, upon subscription, starts a new thread on which it listens for connections,
    /// and yields an `ISocket` object each time a client connects. Cancelling the subscription causes
    /// the listen thread to exit. 
    /// 
    /// `ISocket` values are yielded on `ThreadPool` threads. The observables returned
    /// by these `ISocket` values yield and complete on `ThreadPool` threads
    /// as well. Thus, you must take care to synchronize resources shared by concurrent requests.
    /// </summary>
    public class KayakServer : IObservable<ISocket>
    {
        public IPEndPoint ListenEndPoint { get; private set; }
        int backlog;

        Socket listener;
        IObserver<ISocket> observer;

        ManualResetEvent wh;

        /// <summary>
        /// Constructs a server which binds to port 8080 on all interfaces upon subscription
        /// and maintains a default connection backlog count.
        /// </summary>
        public KayakServer() : this(new IPEndPoint(IPAddress.Any, 8080)) { }

        /// <summary>
        /// Constructs a server which binds to the given local end point upon subscription
        /// and maintains a default connection backlog count.
        /// </summary>
        public KayakServer(IPEndPoint listenEndPoint) : this(listenEndPoint, 1000) { }

        /// <summary>
        /// Constructs a server which binds to the given local end point upon subscription
        /// and maintains the given connection backlog count.
        /// </summary>
        public KayakServer(IPEndPoint listenEndPoint, int backlog)
        {
            ListenEndPoint = listenEndPoint;
            this.backlog = backlog;
            wh = new ManualResetEvent(false);
        }

        public IDisposable Subscribe(IObserver<ISocket> observer)
        {
            if (this.observer != null)
                throw new InvalidOperationException("Listener already has observer.");

            this.observer = observer;

            var listen = new Thread(new ThreadStart(Start));
            listen.Start();

            return Disposable.Create(Stop);
        }

        void Start()
        {
            listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.IP);
            listener.Bind(ListenEndPoint);
            listener.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReceiveTimeout, 10000);
            listener.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.SendTimeout, 10000);
            listener.Listen(backlog);

            try
            {
                while (observer != null)
                {
                    wh.Reset();

                    listener.BeginAccept(NextSocket, null);

                    wh.WaitOne();
                }
            }
            catch (Exception e)
            {
                YieldException(e);
            }
        }

        void NextSocket(IAsyncResult ar)
        {
            Socket socket = null;
            try
            {
                socket = listener.EndAccept(ar);
                Trace.Write("Accepted connection.");
            }
            catch (Exception e)
            {
                YieldException(e);
            }
            finally
            {
                wh.Set();
            }

            if (socket != null)
                observer.OnNext(new DotNetSocket(socket));
        }

        void YieldException(Exception e)
        {
            if (observer != null)
            {
                observer.OnError(e);
                observer = null;
            }
        }

        void Stop()
        {
            observer = null;
            listener.Close();
        }
    }

    class DotNetSocket : ISocket
    {
        Socket socket;

        public DotNetSocket(Socket socket)
        {
            this.socket = socket;
        }

        public IPEndPoint RemoteEndPoint
        {
            get { return (IPEndPoint)socket.RemoteEndPoint; }
        }

        public void Dispose()
        {
            socket.Close();
        }

        #region ISocket Members

        public IObservable<int> Write(byte[] buffer, int offset, int count)
        {
            return socket.SendAsync(buffer, offset, count);
        }

        public IObservable<Unit> WriteFile(string file)
        {
            return socket.SendFileAsync(file);
        }

        public IObservable<int> Read(byte[] buffer, int offset, int count)
        {
            return socket.ReceiveAsync(buffer, offset, count);
        }

        #endregion
    }
}
