using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Kayak.Http
{
    using ResponseFactory =
        Func<
            IHttpServerRequest, 
            IBufferedOutputStreamDelegate, 
            bool, 
            Tuple<IHttpServerResponseInternal, IBufferedOutputStream>>;

    // ties it all together
    class HttpServerTransactionDelegate : IHttpServerTransactionDelegate, IBufferedOutputStreamDelegate
    {
        HttpServer server;
        ResponseFactory responseFactory;
        LinkedList<Tuple<IHttpServerResponseInternal, IBufferedOutputStream>> responses;
        IHttpServerResponseInternal activeResponse;
        ISocket socket;
        bool ended;

        public HttpServerTransactionDelegate(HttpServer server) 
            : this(server, null) { }

        public HttpServerTransactionDelegate(HttpServer server, ResponseFactory responseFactory)
        {
            this.server = server;
            this.responseFactory = responseFactory ?? CreateResponse;
            responses = new LinkedList<Tuple<IHttpServerResponseInternal, IBufferedOutputStream>>();
        }

        public void OnBegin(ISocket socket)
        {
            this.socket = socket;
        }

        Tuple<IHttpServerResponseInternal, IBufferedOutputStream> CreateResponse(
            IHttpServerRequest request,
            IBufferedOutputStreamDelegate del,
            bool shouldKeepAlive)
        {
            IBufferedOutputStream output = new BufferedOutputStream(del);
            IHttpServerResponseInternal response = new Response(output, request, shouldKeepAlive);

            return Tuple.Create(response, output);
        }

        public void OnRequest(IHttpServerRequest request, bool shouldKeepAlive)
        {
            // if socket was ended, throw exception

            if (ended)
                throw new InvalidOperationException("Transaction was ended.");

            
            var responseAndOutput = CreateResponse(request, this, shouldKeepAlive);
            var response = responseAndOutput.Item1;
            var output = responseAndOutput.Item2;

            // if no pending responses, attach
            if (responses.Count == 0)
            {
                activeResponse = response;
                output.Attach(socket);
            }
            // else added to pending
            else
            {
                responses.AddLast(responseAndOutput);
            }

            server.RaiseOnRequest(request, response);
        }

        public void OnEnd()
        {
            ended = true;
        }

        public void OnFlushed(IBufferedOutputStream message)
        {
            // if completed response indicates that connection should be closed,
            // or if the client ended the connection and no further responses
            // are queued, end/shutdown socket
            if (!activeResponse.KeepAlive || (ended && responses.Count == 0))
            {
                socket.End(); // XXX want to also prohibit further reading, set flag and throw exception if more data
                return;
            }
            
            // if pending responses, attach next
            if (responses.Count > 0)
            {
                var next = responses.First.Value;
                var response = next.Item1;
                var output = next.Item2;

                responses.RemoveFirst();
                activeResponse = response;

                output.Attach(socket);
            }

            // if no pending, do nothing (either response was last on connection, or another request is coming later)
        }
    }
}
