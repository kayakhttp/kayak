using System;
using System.Diagnostics;

namespace Kayak.Http
{
    public static partial class Extensions
    {
        public static void AcceptHttp(this IServer server, Func<IHttpServerDelegate> del)
        {
            server.Delegate = new ServerDelegate(del);
        }

        class ServerDelegate : IServerDelegate
        {
            Func<IHttpServerDelegate> del;

            public ServerDelegate(Func<IHttpServerDelegate> del)
            {
                this.del = del;
            }

            public void OnConnection(IServer server, ISocket socket)
            {
                socket.Delegate = new HttpSocketDelegate(socket, del());
            }

            public void OnClose(IServer server)
            {
            }
        }
    }
}
