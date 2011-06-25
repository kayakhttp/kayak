using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Kayak.Http
{
    class HttpResponseDelegate : IHttpResponseDelegate, IResponse
    {
        HttpResponseDelegateState state;
        Action closeConnection;

        HttpResponseHead head;
        IDataProducer body;

        IDataConsumer consumer;
        IDisposable abortBody;

        static IHeaderRenderer defaultRenderer;
        static HttpResponseDelegate() {
            defaultRenderer = new HttpResponseHeaderRenderer();
        }

        internal IHeaderRenderer renderer = defaultRenderer;

        public HttpResponseDelegate(bool prohibitBody, bool shouldKeepAlive, Action closeConnection)
        {
            state = new HttpResponseDelegateState(prohibitBody, shouldKeepAlive);
            this.closeConnection = closeConnection;
        }

        public void WriteContinue()
        {
            var writeContinue = false;

            state.OnWriteContinue(out writeContinue);

            if (writeContinue)
                RenderContinue();
        }

        public void OnResponse(HttpResponseHead head, IDataProducer body)
        {
            var writeResponse = false;
            var hasBody = body != null;

            state.OnResponse(hasBody, out writeResponse);

            this.head = head;
            this.body = body;

            if (writeResponse)
                BeginResponse();
        }

        public IDisposable Connect(IDataConsumer consumer)
        {
            if (consumer == null)
                throw new ArgumentNullException();

            this.consumer = consumer;

            bool writeContinue = false;
            bool begin = false;

            state.OnConnect(out begin, out writeContinue);

            if (writeContinue)
                RenderContinue();

            if (begin)
                BeginResponse();

            return new Disposable(Abort);
        }

        void Abort()
        {
            // should cause OnResponse to throw
            state.OnAbort();

            if (abortBody != null)
                abortBody.Dispose();
        }

        void BeginResponse()
        {
            RenderHeaders();

            if (body == null)
                consumer.OnEnd();
            else
                abortBody = body.Connect(consumer);
        }

        static ArraySegment<byte> oneHunderedContinue =
            new ArraySegment<byte>(Encoding.ASCII.GetBytes("100 Continue\r\n\r\n"));

        void RenderContinue()
        {
            consumer.OnData(oneHunderedContinue, null);
        }

        void RenderHeaders()
        {
            var headers = head.Headers;

            bool indicateConnection;
            bool indicateConnectionClose;

            bool givenConnection = headers.ContainsKey("Connection");
            bool givenConnectionClose = givenConnection && headers["Connection"] == "close";

            state.OnRenderHeaders(
                givenConnection,
                givenConnectionClose,
                out indicateConnection,
                out indicateConnectionClose);

            if (indicateConnection)
            {
                headers["Connection"] = indicateConnectionClose ? "close" : "keep-alive";
                if (indicateConnectionClose) 
                    closeConnection();
            }

            renderer.Render(consumer, head);
        }
    }
}
