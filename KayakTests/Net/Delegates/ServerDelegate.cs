using System;
using Kayak;

namespace KayakTests.Net
{
    class ServerDelegate : IDisposable
    {
        IServer server;
        public Action<ISocket> OnConnection;
        public Action OnClose;

        public int NumOnConnectionEvents;
        public int NumOnCloseEvents;

        public ServerDelegate(IServer server)
        {
            this.server = server;
            server.OnConnection += new EventHandler<ConnectionEventArgs>(server_OnConnection);
            server.OnClose += new EventHandler(server_OnClose);
        }

        public void Dispose()
        {
            server.OnConnection -= server_OnConnection;
            server.OnClose -= server_OnClose;
            server = null;
        }

        void server_OnConnection(object sender, ConnectionEventArgs e)
        {
            NumOnConnectionEvents++;

            if (OnConnection != null)
            {
                OnConnection(e.Socket);
            }
            else e.Socket.Dispose();
        }

        void server_OnClose(object sender, EventArgs e)
        {
            NumOnCloseEvents++;

            if (OnClose != null)
                OnClose();
        }
    }
}
