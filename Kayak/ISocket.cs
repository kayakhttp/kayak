using System;
using System.Net;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace Kayak
{
    public interface IScheduler
    {
        event EventHandler OnStarted;
        event EventHandler OnStopped;

        void Start();
        void Stop();

        void Post(Action action);
    }

    // (do not retain, impl could have global instance)
    public class ConnectionEventArgs : EventArgs
    {
        public ISocket Socket { get; private set; }

        public ConnectionEventArgs(ISocket socket)
        {
            Socket = socket;
        }
    }

    public interface IServer : IDisposable
    {
        IPEndPoint ListenEndPoint { get; }

        event EventHandler<ConnectionEventArgs> OnConnection;
        event EventHandler OnClose;

        void Listen(IPEndPoint ep);
        void Close();
    }

    public class DataEventArgs : EventArgs
    {
        public ArraySegment<byte> Data;
        public Action Continuation;

        // someone must *always* set this var when a dataevent is handled
        public bool WillInvokeContinuation;
    }

    public class ExceptionEventArgs : EventArgs
    {
        public Exception Exception { get; internal set; }
    }

    public interface ISocket : IDisposable
    {
        event EventHandler OnConnected;
        event EventHandler<DataEventArgs> OnData;
        event EventHandler OnEnd;
        event EventHandler OnTimeout;
        event EventHandler<ExceptionEventArgs> OnError;
        event EventHandler OnClose;

        IPEndPoint RemoteEndPoint { get; }
        void SetNoDelay(bool noDelay);
        //void SetKeepAlive(bool keepAlive, int delay);

        void Connect(IPEndPoint ep);
        bool Write(ArraySegment<byte> data, Action continuation);
        void End(); // send FIN
    }

    public interface IStream : IDisposable
    {
        event EventHandler<DataEventArgs> OnData;
        event EventHandler OnEnd;
        event EventHandler<ExceptionEventArgs> OnError;
        event EventHandler OnClose;

        bool CanRead { get; }
        bool CanWrite { get; }

        void End(); 
        bool Write(ArraySegment<byte> data, Action continuation);
    }
}
