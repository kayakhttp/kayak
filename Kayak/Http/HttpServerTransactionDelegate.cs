using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Kayak.Http
{
    // ties it all together
    class HttpServerTransactionDelegate : IHttpServerTransactionDelegate, IOutputStreamDelegate
    {
        HttpServer server;
        LinkedList<Response> responses;
        Response activeResponse;
        ISocket socket;
        bool ended;

        public HttpServerTransactionDelegate(HttpServer server)
        {
            this.server = server;
            responses = new LinkedList<Response>();
        }

        public void OnBegin(ISocket socket)
        {
            this.socket = socket;
        }

        public void OnRequest(IHttpServerRequest request, bool shouldKeepAlive)
        {
            // if socket was ended, throw exception

            if (ended)
                throw new InvalidOperationException("Transaction was ended.");

            var response = new Response(this, request, shouldKeepAlive);

            // if no pending responses, attach
            if (responses.Count == 0)
            {
                activeResponse = response;
                ((IOutputStream)activeResponse).Attach(socket);
            }
            // else added to pending
            else
            {
                responses.AddLast(response);
            }

            server.RaiseOnRequest(request, response);
        }


        public void OnEnd()
        {
            ended = true;
        }

        public void OnFlushed(IOutputStream message)
        {
            var response = (Response)message;
            // if completed response was last, end/shutdown socket
            if (!response.KeepAlive || (ended && responses.Count == 0))
                socket.End(); // XXX want to also prohibit further reading, set flag and throw exception if more data

            // if pending responses, attach next
            if (responses.Count > 0)
            {
                var next = responses.First.Value;

                responses.RemoveFirst();
                activeResponse = next;

                ((IOutputStream)activeResponse).Attach(socket);
            }

            // if no pending, do nothing (either response was last on connection, or another request is coming later)
        }
    }
}
