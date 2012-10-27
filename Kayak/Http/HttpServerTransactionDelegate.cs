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
        IOutputQueue outputQueue;

        public HttpServerTransactionDelegate(IHttpRequestDelegate requestDelegate, IOutputQueue outputQueue)
        {
            this.requestDelegate = requestDelegate;
            this.outputQueue = outputQueue;
        }

        public void OnRequest(IHttpServerTransaction transaction, HttpRequestHead request, bool shouldKeepAlive)
        {
            AddXFF(ref request, transaction.RemoteEndPoint);

            var expectContinue = request.IsContinueExpected();
            var ignoreResponseBody = request.Method != null && request.Method.ToUpperInvariant() == "HEAD";

            currentContext = new TransactionContext(outputQueue, 
                expectContinue, ignoreResponseBody, shouldKeepAlive);

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
            try
            {
                currentContext.RequestBody.OnError(e);
            }
            finally
            {
                outputQueue.Dispose();
            }
        }

        public void OnEnd(IHttpServerTransaction transaction)
        {
            outputQueue.End();
        }

        public void OnClose(IHttpServerTransaction transaction)
        {
            outputQueue.Dispose();
            // XXX return self to freelist
        }

        void AddXFF(ref HttpRequestHead request, IPEndPoint remoteEndPoint)
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
