using System;
using System.Net;

namespace Kayak
{
    public interface IScheduler : IDisposable
    {
        void Post(Action action);
        void Start(ISchedulerDelegate del);
        void Stop();
    }

    public interface ISchedulerDelegate
    {
        void OnException(IScheduler scheduler, Exception e);
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
