using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Kayak.Http
{
    class HttpResponseDelegate : IHttpResponseDelegate, IDataProducer
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

        public HttpResponseDelegate(bool prohibitBody, bool shouldKeepAlive, bool expectContinue, Action closeConnection)
        {
            state = HttpResponseDelegateState.Create(prohibitBody, shouldKeepAlive, expectContinue);
            this.closeConnection = closeConnection;
        }

        public void OnResponse(HttpResponseHead head, IDataProducer body)
        {
            var begin = false;
            var hasBody = body != null;

            state.OnResponse(hasBody, out begin);

            this.head = head;
            this.body = body;

            if (begin)
                Begin();
        }

        public IDisposable Connect(IDataConsumer consumer)
        {
            if (consumer == null)
                throw new ArgumentNullException();

            this.consumer = consumer;

            bool begin = false;

            state.OnConnect(out begin);

            if (begin)
                Begin();

            return new Disposable(Abort);
        }

        void Abort()
        {
            // should cause OnResponse to throw
            state.OnAbort();

            if (abortBody != null)
                abortBody.Dispose();
        }

        void Begin()
        {
            RenderHeaders();

            if (body == null)
                consumer.OnEnd();
            else
                abortBody = body.Connect(consumer);
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
