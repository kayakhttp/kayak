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

    public interface IServer : IDisposable
    {
        IPEndPoint ListenEndPoint { get; }

        event EventHandler<ConnectionEventArgs> OnConnection;
        event EventHandler OnClose;

        void Listen(IPEndPoint ep);
        void Close();
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
        // successful connection
        event EventHandler OnConnected;

        // got some data. see DataEventArgs.
        event EventHandler<DataEventArgs> OnData;

        // received FIN or RST
        event EventHandler OnEnd;
        
        // error while reading or writing. OnClose will be raised next.
        event EventHandler<ExceptionEventArgs> OnError;
        
        // raised after both OnEnd is raise and End() is called.
        // also raised after OnError is raised.
        // handler should dispose the socket.
        event EventHandler OnClose; 

        IPEndPoint RemoteEndPoint { get; }
        
        // connect to the EP
        void Connect(IPEndPoint ep);

        // write some data. if you provide a continuation and Write() returns true,
        // don't call Write() again until the continuation is invoked. this is a
        // mechanism for indicating that the data has been buffered in userspace
        // and that further writes should be delayed to prevent a potentially 
        // unbounded buffer.
        bool Write(ArraySegment<byte> data, Action continuation);

        void End(); // send FIN

        // future:

        // haven't heard from peer lately. 
        // this is just a timer, you have to manually end/dispose the connection
        // if that's what needs to happen.
        // event EventHandler OnTimeout;

        // fancy bits.
        // void SetNoDelay(bool noDelay);
        // void SetKeepAlive(bool keepAlive, int delay);
    }

    interface IOutputStream
    {
        bool Write(ArraySegment<byte> data, Action continuation);
        void End();
    }

    // do not retain these EventArgs classes, impl could have global instances.
   
    // the server got a connection!
    public class ConnectionEventArgs : EventArgs
    {
        public ISocket Socket { get; private set; }

        public ConnectionEventArgs(ISocket socket)
        {
            Socket = socket;
        }
    }

    // the socket received data!
    public class DataEventArgs : EventArgs
    {
        // the data received.
        public ArraySegment<byte> Data;

        // invoke when you're willing to accept more data. never null.
        public Action Continuation;

        // false by default. set to true if handler will invoke the continuation.
        // this is a mechanism for throttling back the incoming data stream.
        // in general, set this to true if you've buffered the data in userspace.
        public bool WillInvokeContinuation;
    }

    // uh oh!
    public class ExceptionEventArgs : EventArgs
    {
        public Exception Exception { get; internal set; }
    }
}
