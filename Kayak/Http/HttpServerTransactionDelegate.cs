using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Kayak.Http
{
    using RequestDelegate =
        Action<IHttpServerRequest, IHttpServerResponse>;

    using ResponseFactory =
        Func<
            IHttpServerRequest, 
            IBufferedOutputStreamDelegate, 
            bool, 
            Tuple<IHttpServerResponseInternal, IBufferedOutputStream>>;
    using System.Diagnostics;

    // ties it all together
    class HttpServerTransactionDelegate : IHttpServerTransactionDelegate, IBufferedOutputStreamDelegate
    {
        RequestDelegate requestDelegate;
        ResponseFactory responseFactory;

        LinkedList<Tuple<IHttpServerResponseInternal, IBufferedOutputStream>> responses;
        IHttpServerResponseInternal activeResponse;
        ISocket socket;
        bool ended;
        bool shutdown;

        public HttpServerTransactionDelegate(RequestDelegate requestDelegate) 
            : this(requestDelegate, null) { }

        public HttpServerTransactionDelegate(RequestDelegate requestDelegate, ResponseFactory responseFactory)
        {
            this.requestDelegate = requestDelegate;
            this.responseFactory = responseFactory ?? CreateResponse;
            responses = new LinkedList<Tuple<IHttpServerResponseInternal, IBufferedOutputStream>>();
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

        public void OnBegin(ISocket socket)
        {
            ended = false;
            shutdown = false;
            this.socket = socket;
        }

        public void OnRequest(IHttpServerRequest request, bool shouldKeepAlive)
        {
            if (ended)
                throw new InvalidOperationException("Transaction was ended.");

            // XXX does this flag make sense?
            if (shutdown)
                throw new InvalidOperationException("Socket was shutdown."); 
            
            var responseAndOutput = responseFactory(request, this, shouldKeepAlive);
            var response = responseAndOutput.Item1;
            var output = responseAndOutput.Item2;

            // if no active response, attach
            if (activeResponse == null)
            {
                Debug.WriteLine("Transaction: no active response, attaching");
                activeResponse = response;
                output.Attach(socket);
            }
            // else add to pending
            else
            {
                Debug.WriteLine("Transaction: active response, queuing");
                responses.AddLast(responseAndOutput);
            }

            requestDelegate(request, response);
        }

        public void OnEnd()
        {
            ended = true;
        }

        public void OnDrained(IBufferedOutputStream message)
        {
            var keepAlive = activeResponse.KeepAlive;

            // if completed response indicates that connection should be closed,
            // or if the client ended the connection and no further responses
            // are queued, end/shutdown socket
            if (!keepAlive || (ended && responses.Count == 0))
            {
                Debug.WriteLine("Transaction: ending socket");
                shutdown = true;
                socket.End();
            }
            // if pending responses, attach next
            else if (responses.Count > 0)
            {
                Debug.WriteLine("Transaction: attaching next pending response");
                var next = responses.First.Value;
                var response = next.Item1;
                var output = next.Item2;
                responses.RemoveFirst();

                activeResponse = response;
                output.Attach(socket);
            }
            else
            {
                Debug.WriteLine("Transaction: no pending responses");
                // if no pending, do nothing (either response was last on connection, or another request is coming later)
                activeResponse = null;
            }
        }
    }
}
