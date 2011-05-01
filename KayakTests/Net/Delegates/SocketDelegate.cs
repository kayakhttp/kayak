using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Kayak;

namespace KayakTests.Net
{
    class SocketDelegate
    {
        public Action OnTimeoutAction;
        public Action<Exception> OnErrorAction;
        public Action OnEndAction;
        public Func<ArraySegment<byte>, Action, bool> OnDataAction;
        public Action OnConnectedAction;
        public Action OnCloseAction;

        public Exception Exception;
        public int NumOnConnectedEvents;
        public int NumOnEndEvents;
        public int NumOnCloseEvents;

        public DataBuffer Buffer;

        public SocketDelegate(ISocket socket)
        {
            Buffer = new DataBuffer();
        }

        void OnTimeout(IServer server)
        {
            if (OnTimeoutAction != null)
                OnTimeoutAction();
        }

        void OnError(IServer server, Exception e)
        {
            Exception = e;
            if (OnErrorAction != null)
                OnErrorAction(e);
        }

        void OnEnd(IServer server)
        {
            NumOnEndEvents++;
            if (OnEndAction != null)
                OnEndAction();
        }

        bool OnData(IServer server, ArraySegment<byte> data, Action continuation)
        {
            Buffer.AddToBuffer(data);

            if (OnDataAction != null)
                return OnDataAction(data, continuation);

            return false;
        }


        void OnConnected(IServer server)
        {
            NumOnConnectedEvents++;
            if (OnConnectedAction != null)
                OnConnectedAction();
        }

        void OnClose(IServer server)
        {
            NumOnCloseEvents++;
            if (OnCloseAction != null)
                OnCloseAction();
        }
    }
}
