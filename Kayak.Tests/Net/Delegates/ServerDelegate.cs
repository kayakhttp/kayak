using System;
using Kayak;

namespace Kayak.Tests.Net
{
    class ServerDelegate : IServerDelegate
    {
        public Func<IServer, ISocket, ISocketDelegate> OnConnectionAction = null;
        public Action OnCloseAction = null;

        public int NumOnConnectionEvents;
        public int NumOnCloseEvents;

        public ISocketDelegate OnConnection(IServer server, ISocket socket)
        {
            NumOnConnectionEvents++;

            if (OnConnectionAction != null)
            {
                return OnConnectionAction(server, socket);
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
