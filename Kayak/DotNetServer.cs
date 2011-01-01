using System;
using System.Net;
using System.Net.Sockets;
using Coroutine;

namespace Kayak
{
    /// <summary>
    /// DotNetServer is a simple IKayakServer implementation using `System.Net.Sockets.Socket`.
    /// 
    /// `ISocket` values are yielded on `ThreadPool` threads as determined by the Socket object. 
    /// The operations that these `ISocket` values expose yield on `ThreadPool` threads
    /// as well. Thus, you must take care to synchronize resources shared by concurrent requests.
    /// </summary>
    public class DotNetServer : IKayakServer
    {
        public IPEndPoint ListenEndPoint { get; private set; }
        int backlog;

        Socket listener;
        
        /// <summary>
        /// Constructs a server which binds to port 8080 on all interfaces upon subscription
        /// and maintains a default connection backlog count.
        /// </summary>
        public DotNetServer() : this(new IPEndPoint(IPAddress.Any, 8080)) { }

        /// <summary>
        /// Constructs a server which binds to the given local end point upon subscription
        /// and maintains a default connection backlog count.
        /// </summary>
        public DotNetServer(IPEndPoint listenEndPoint) : this(listenEndPoint, 1000) { }

        /// <summary>
        /// Constructs a server which binds to the given local end point upon subscription
        /// and maintains the given connection backlog count.
        /// </summary>
        public DotNetServer(IPEndPoint listenEndPoint, int backlog)
        {
            ListenEndPoint = listenEndPoint;
            this.backlog = backlog;
        }

        public IDisposable Start()
        {
            listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.IP);
            listener.Bind(ListenEndPoint);
            listener.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReceiveTimeout, 10000);
            listener.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.SendTimeout, 10000);
            listener.Listen(backlog);

            return new Stopper(listener);
        }

        class Stopper : IDisposable
        {
            Socket listener;
            public Stopper(Socket listener)
            {
                this.listener = listener;
            }
            public void Dispose()
            {
                listener.Close();
            }
        }

        public Action<Action<ISocket>> GetConnection()
        {
            return r => listener.BeginAccept(iasr =>
            {
                try
                {
                    r(new DotNetSocket(listener.EndAccept(iasr)));
                }
                catch (Exception e)
                {
                    Console.WriteLine("Exception while accepting connection.");
                    Console.Out.WriteException(e);
                }
                
            }, null);
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

        public Action<Action<int>, Action<Exception>> Write(byte[] buffer, int offset, int count)
        {
            return Continuation.FromAsync<int>(
                (c, s) => socket.BeginSend(buffer, offset, count, SocketFlags.None, c, s), 
                socket.EndSend);
        }

        public Action<Action, Action<Exception>> WriteFile(string file)
        {
            return Continuation.FromAsync(
                (c, s) => socket.BeginSendFile(file, c, s),
                iasr => { socket.EndSendFile(iasr); });
        }

        public Action<Action<int>, Action<Exception>> Read(byte[] buffer, int offset, int count)
        {
            return Continuation.FromAsync<int>(
                (c, s) => socket.BeginReceive(buffer, offset, count, SocketFlags.None, c, s),
                socket.EndSend);
        }
    }
}
