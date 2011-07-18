using System;
using System.Net;

namespace Kayak
{
    public interface ISchedulerFactory
    {
        IScheduler Create(ISchedulerDelegate del);
    }

    public interface IScheduler : IDisposable
    {
        void Post(Action action);
        void Start();
        void Stop();
    }

    public interface ISchedulerDelegate
    {
        void OnException(IScheduler scheduler, Exception e);
        void OnStop(IScheduler scheduler);
    }

    public static class KayakScheduler
    {
        readonly static ISchedulerFactory factory = new DefaultKayakSchedulerFactory();
        public static ISchedulerFactory Factory { get { return factory; } }
    }

    class DefaultKayakSchedulerFactory : ISchedulerFactory
    {
        public IScheduler Create(ISchedulerDelegate del)
        {
            return new DefaultKayakScheduler(del);
        }
    }

    public interface IServerFactory
    {
        IServer Create(IServerDelegate del, IScheduler scheduler);
    }
    
    public interface IServer : IDisposable
    {
        IDisposable Listen(IPEndPoint ep);
    }

    public interface IServerDelegate
    {
        ISocketDelegate OnConnection(IServer server, ISocket socket);
        void OnClose(IServer server);
    }

    public static class KayakServer
    {
        readonly static DefaultKayakServerFactory factory = new DefaultKayakServerFactory();
        public static IServerFactory Factory { get { return factory; } }
    }

    class DefaultKayakServerFactory : IServerFactory
    {
        public IServer Create(IServerDelegate del, IScheduler scheduler)
        {
            return new DefaultKayakServer(del, scheduler);
        }
    }

    public interface ISocketFactory
    {
        ISocket Create(ISocketDelegate del, IScheduler scheduler);
    }

    public interface ISocketDelegate
    {
        void OnConnected(ISocket socket);
        bool OnData(ISocket socket, ArraySegment<byte> data, Action continuation);
        void OnEnd(ISocket socket);
        void OnError(ISocket socket, Exception e);
        void OnClose(ISocket socket);
    }

    public interface ISocket : IDisposable
    {
        IPEndPoint RemoteEndPoint { get; }
        void Connect(IPEndPoint ep);
        bool Write(ArraySegment<byte> data, Action continuation);
        void End();
    }

    public static class KayakSocket
    {
        readonly static ISocketFactory factory = new DefaultKayakSocketFactory();
        public static ISocketFactory Factory { get { return factory; } }
    }

    class DefaultKayakSocketFactory : ISocketFactory
    {
        public ISocket Create(ISocketDelegate del, IScheduler scheduler)
        {
            return new DefaultKayakSocket(del, scheduler);
        }
    }

    public interface IDataProducer
    {
        IDisposable Connect(IDataConsumer channel);
    }

    public interface IDataConsumer
    {
        void OnError(Exception e);
        bool OnData(ArraySegment<byte> data, Action continuation);
        void OnEnd();
    }
}
