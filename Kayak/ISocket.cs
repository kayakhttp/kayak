using System;
using System.Net;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace Kayak
{
    // (do not retain, impl could have global instance)
    public class ConnectionEventArgs : EventArgs
    {
        public ISocket Socket { get; internal set; }
    }

    public interface IServer
    {
        IServerDelegate Delegate { get; set; }
        IPEndPoint ListenEndPoint { get; }

        event EventHandler<ConnectionEventArgs> OnConnection;
        event EventHandler OnClose;

        void Listen(IPEndPoint ep);
        void Close();
    }

    public interface IServerDelegate
    {
        void OnConnection(IServer server, ISocket socket);
        void OnClose(IServer server);
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

    public interface ISocketDelegate
    {
        void OnConnected(ISocket socket);
        bool OnData(ISocket socket, ArraySegment<byte> data, Action continuation);
        void OnEnd(ISocket socket); // received FIN
        void OnTimeout(ISocket socket);

        void OnError(ISocket socket, Exception e);
        void OnClose(ISocket socket);
    }

    public interface IStream : IDisposable
    {
        IStreamDelegate Delegate { get; set; }

        bool CanRead { get; }
        bool CanWrite { get; }

        void End(); 
        bool Write(ArraySegment<byte> data, Action continuation);
    }

    public interface IStreamDelegate
    {
        bool OnData(IStream stream, ArraySegment<byte> data, Action continuation);
        void OnEnd(IStream stream);
        void OnError(IStream stream, Exception exception);
        void OnClose(IStream stream);
    }
}
