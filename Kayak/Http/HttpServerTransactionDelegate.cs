using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Kayak.Http
{
    // ties it all together
    class HttpServerTransactionDelegate : IHttpServerTransactionDelegate
    {
        HttpServer server;
        LinkedList<Response> responses;
        Response activeResponse;

        public HttpServerTransactionDelegate(HttpServer server)
        {
            this.server = server;
        }

        public void OnRequest(IHttpServerRequest request, bool shouldKeepAlive)
        {
            // if socket was ended, throw exception

            var output = new AttachableStream(OnResponseCompleted);
            var response = new Response(request, shouldKeepAlive, output);

            // if no pending responses, attach
            // else added to pending

            server.RaiseOnRequest(request, response);
        }

        public void OnEnd()
        {
            // mark most recent response as last
        }

        void OnResponseCompleted()
        {
            // if completed response was last, end/shutdown socket
            // if pending responses, attach
            // if no pending, do nothing (wait)
        }
    }
}
