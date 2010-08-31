using System;
using System.IO;
using System.Net;
using System.Disposables;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Linq;
using System.Threading;

namespace Kayak
{
    public class KayakServer : IObservable<ISocket>
    {
        public IPEndPoint ListenEndPoint { get; private set; }
        int backlog;

        Socket listener;
        Func<IObservable<Socket>> accept;
        IObserver<ISocket> observer;

        ManualResetEvent wh;

        public KayakServer() : this(new IPEndPoint(IPAddress.Any, 8080)) { }
        public KayakServer(IPEndPoint listenEndPoint) : this(listenEndPoint, 1000) { }
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

            accept = Observable.FromAsyncPattern<Socket>(listener.BeginAccept, listener.EndAccept);

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
            try
            {
                var s = listener.EndAccept(ar);
                observer.OnNext(new DotNetSocket(s));
            }
            catch (Exception e)
            {
                YieldException(e);
            }
            finally
            {
                wh.Set();
            }
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

        public IObservable<int> WriteFile(string file, int offset, int count)
        {
            if (offset != 0)
                throw new ArgumentException("Non-zero offset is not supported.");
            if (count != 0)
                throw new ArgumentException("Non-zero count is not supported.");

            return socket.SendFileAsync(file);
        }

        public IObservable<int> Read(byte[] buffer, int offset, int count)
        {
            return socket.ReceiveAsync(buffer, offset, count);
        }

        #endregion
    }
}
