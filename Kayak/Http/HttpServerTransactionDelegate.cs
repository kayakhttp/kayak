using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Net;

namespace Kayak.Http
{
    class HttpServerTransactionDelegate : IHttpServerTransactionDelegate
    {
        IHttpRequestDelegate requestDelegate;
        TransactionContext currentContext;
        ITransactionSegment lastSegment;

        public HttpServerTransactionDelegate(IHttpRequestDelegate requestDelegate)
        {
            this.requestDelegate = requestDelegate;
        }

        void QueueSegment(ITransactionSegment segment)
        {
            if (lastSegment != null)
                lastSegment.AttachNext(segment);

            lastSegment = segment;
        }

        public void OnRequest(IHttpServerTransaction transaction, HttpRequestHead request, bool shouldKeepAlive)
        {
            AddXFF(request, transaction.RemoteEndPoint);

            var expectContinue = request.IsContinueExpected();
            var ignoreResponseBody = request.Method != null && request.Method.ToUpperInvariant() == "HEAD";

            currentContext = new TransactionContext(expectContinue, ignoreResponseBody, shouldKeepAlive);

            if (lastSegment == null)
                currentContext.Segment.AttachTransaction(transaction);

            QueueSegment(currentContext.Segment);
            requestDelegate.OnRequest(request, currentContext.RequestBody, currentContext);
        }

        public bool OnRequestData(IHttpServerTransaction transaction, ArraySegment<byte> data, Action continuation)
        {
            return currentContext.RequestBody.OnData(data, continuation);
        }

        public void OnRequestEnd(IHttpServerTransaction transaction)
        {
            currentContext.RequestBody.OnEnd();
        }

        public void OnError(IHttpServerTransaction transaction, Exception e)
        {
            currentContext.RequestBody.OnError(e);
        }

        public void OnEnd(IHttpServerTransaction transaction)
        {
            QueueSegment(new EndSegment());
        }

        public void OnClose(IHttpServerTransaction transaction)
        {
            transaction.Dispose();
            // XXX return self to freelist
        }

        class TransactionContext : IHttpResponseDelegate
        {
            public DataSubject RequestBody;
            public ResponseSegment Segment;
            
            bool expectContinue, ignoreResponseBody, shouldKeepAlive;
            bool gotConnectRequestBody, gotOnResponse;

            public TransactionContext(bool expectContinue, bool ignoreResponseBody, bool shouldKeepAlive)
            {
                RequestBody = new DataSubject(ConnectRequestBody);
                Segment = new ResponseSegment();

                this.expectContinue = expectContinue;
                this.ignoreResponseBody = ignoreResponseBody;
                this.shouldKeepAlive = shouldKeepAlive;
            }

            IDisposable ConnectRequestBody()
            {
                if (gotConnectRequestBody) 
                    throw new InvalidOperationException("Request body was already connected.");

                gotConnectRequestBody = true;

                if (expectContinue)
                    Segment.WriteContinue();

                return new Disposable(() =>
                {
                    // XXX what to do?
                    // ideally we stop reading from the socket. 
                    // equivalent to a parse error
                    // dispose transaction
                });
            }

            public void OnResponse(HttpResponseHead head, IDataProducer body)
            {
                // XXX still need to better account for Connection: close.
                // this should cause the queue to drop pending responses
                // perhaps segment.Abort which disposes transation

                if (!shouldKeepAlive)
                {
                    if (head.Headers == null)
                        head.Headers = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);

                    head.Headers["Connection"] = "close";
                }

                Segment.WriteResponse(head, ignoreResponseBody ? null : body);
            }
        }

        void AddXFF(HttpRequestHead request, IPEndPoint remoteEndPoint)
        {
            if (remoteEndPoint != null)
            {
                if (request.Headers == null)
                    request.Headers = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);
                    
                if (request.Headers.ContainsKey("X-Forwarded-For"))
                {
                    request.Headers["X-Forwarded-For"] += "," + remoteEndPoint.Address.ToString();
                }
                else
                {
                    request.Headers["X-Forwarded-For"] = remoteEndPoint.Address.ToString();
                }
            }
        }
    }
}
