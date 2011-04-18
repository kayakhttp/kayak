using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Kayak.Http
{
    class HttpServer : IHttpServer
    {
        int listeners;
        EventHandler<HttpRequestEventArgs> onRequest;

        public event EventHandler<HttpRequestEventArgs> OnRequest
        {
            add
            {
                onRequest = (EventHandler<HttpRequestEventArgs>)Delegate.Combine(onRequest, value);
                listeners++;

                if (listeners == 1)
                {
                    server.OnConnection += OnConnection;
                }
            }

            remove
            {
                onRequest = (EventHandler<HttpRequestEventArgs>)Delegate.Remove(onRequest, value);
                listeners--;

                if (listeners == 0)
                {
                    server.OnConnection -= OnConnection;
                }
            }
        }

        IServer server;

        public HttpServer(IServer server)
        {
            this.server = server;
        }

        void OnConnection(object sender, ConnectionEventArgs e)
        {
            var socket = e.Socket;

            // XXX freelist
            var del = new HttpServerSocketDelegate(socket, new HttpServerTransactionDelegate(this));
        }

        internal void RaiseOnRequest(IHttpServerRequest req, IHttpServerResponse res)
        {
            if (onRequest != null)
                onRequest(this, new HttpRequestEventArgs() { Request = req, Response = res });
            else
                res.End();
        }
    }

}
