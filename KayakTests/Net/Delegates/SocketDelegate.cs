using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Kayak;

namespace KayakTests.Net
{
    class SocketDelegate : IDisposable
    {
        ISocket socket;

        public Action OnTimeout;
        public Action<Exception> OnError;
        public Action OnEnd;
        public Func<ArraySegment<byte>, Action, bool> OnData;
        public Action OnConnected;
        public Action OnClose;

        public Exception Exception;
        public int NumOnConnectedEvents;
        public int NumOnEndEvents;
        public int NumOnCloseEvents;

        public DataBuffer Buffer;

        public SocketDelegate(ISocket socket)
        {
            this.socket = socket;
            socket.OnClose += socket_OnClose;
            socket.OnConnected += socket_OnConnected;
            socket.OnData += socket_OnData;
            socket.OnEnd += socket_OnEnd;
            socket.OnError += socket_OnError;
            //socket.OnTimeout += socket_OnTimeout;
            Buffer = new DataBuffer();
        }

        public void Dispose()
        {
            socket.OnClose -= socket_OnClose;
            socket.OnConnected -= socket_OnConnected;
            socket.OnData -= socket_OnData;
            socket.OnEnd -= socket_OnEnd;
            socket.OnError -= socket_OnError;
            //socket.OnTimeout -= socket_OnTimeout;
            socket = null;
        }

        void socket_OnTimeout(object sender, EventArgs e)
        {
            if (OnTimeout != null)
                OnTimeout();
        }

        void socket_OnError(object sender, ExceptionEventArgs e)
        {
            Exception = e.Exception;
            if (OnError != null)
                OnError(e.Exception);
        }

        void socket_OnEnd(object sender, EventArgs e)
        {
            NumOnEndEvents++;
            if (OnEnd != null)
                OnEnd();
        }

        void socket_OnData(object sender, DataEventArgs e)
        {
            e.WillInvokeContinuation = false;

            Buffer.AddToBuffer(e.Data);

            if (OnData != null)
                e.WillInvokeContinuation = OnData(e.Data, e.Continuation);
        }


        void socket_OnConnected(object sender, EventArgs e)
        {
            NumOnConnectedEvents++;
            if (OnConnected != null)
                OnConnected();
        }

        void socket_OnClose(object sender, EventArgs e)
        {
            NumOnCloseEvents++;
            if (OnClose != null)
                OnClose();
        }
    }
}
