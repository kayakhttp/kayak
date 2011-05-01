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
        IDisposable Start();
        void Post(Action action);
    }

    public interface ISchedulerDelegate
    {
        void OnStarted(IScheduler scheduler);
        void OnStopped(IScheduler scheduler);
        void OnException(IScheduler scheduler, Exception e);
    }

    public interface IServerFactory
    {
        IServer Create(IServerDelegate del, IScheduler scheduler);
    }
    
    public interface IServer : IDisposable
    {
        IPEndPoint ListenEndPoint { get; }
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
        // OnWriteError? (i.e., cancel writing)
    }

    public interface ISocket : IDisposable
    {
        IPEndPoint RemoteEndPoint { get; }
        void Connect(IPEndPoint ep);
        bool Write(ArraySegment<byte> data, Action continuation);
        void End();
    }

    interface IOutputStream
    {
        bool Write(ArraySegment<byte> data, Action continuation);
        void End();
    }
}
