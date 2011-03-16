using System;
using System.Diagnostics;

namespace Kayak.Http
{
    public static partial class Extensions
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
                server.OnConnection += OnConnection;
            }

            void OnConnection(object sender, ConnectionEventArgs e)
            {
                var socket = e.Socket;

                // XXX freelist
                var del = new HttpSocketDelegate((req, res) =>
                {
                    if (onRequest == null)
                        onRequest(this, new HttpRequestEventArgs() { Request = req, Response = res });
                    else
                        socket.End();
                });

            }
        }

        public static IHttpServer AsHttpServer(this IServer server)
        {
            return new HttpServer(server);
        }
    }
}
