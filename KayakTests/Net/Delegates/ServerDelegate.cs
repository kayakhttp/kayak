using System;
using Kayak;

namespace KayakTests.Net
{
    class ServerDelegate : IServerDelegate
    {
        IServer server;
        public Func<ISocket, ISchedulerDelegate> OnConnectionAction;
        public Action OnCloseAction;

        public int NumOnConnectionEvents;
        public int NumOnCloseEvents;

        public ISocketDelegate OnConnection(IServer server, ISocket socket)
        {
            NumOnConnectionEvents++;

            if (OnConnectionAction != null)
            {
                return OnConnectionAction(socket);
            }
            else
            {
                socket.Dispose();
                return null;
            }
        }

        public void OnClose(IServer server)
        {
            NumOnCloseEvents++;

            if (OnCloseAction != null)
                OnCloseAction();
        }
    }
}
