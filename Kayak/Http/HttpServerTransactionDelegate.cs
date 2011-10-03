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
        IPAddress remoteAddress;
        IHttpResponseDelegateFactory responseDelegateFactory;
        IHttpRequestDelegate requestDelegate;

        IObserver<IDataProducer> observer;
        IDataConsumer requestBody;
        bool closeConnection;

        public HttpServerTransactionDelegate(
            IPAddress remoteAddress,
            IHttpResponseDelegateFactory responseDelegateFactory, 
            IHttpRequestDelegate requestDelegate)
        {
            this.remoteAddress = remoteAddress;
            this.responseDelegateFactory = responseDelegateFactory;
            this.requestDelegate = requestDelegate;
        }

        public IDisposable Subscribe(IObserver<IDataProducer> observer)
        {
            this.observer = observer;

            return new Disposable(() =>
            {
                // i.e., output queue wants no more
                // this won't happen with the current implementation
            });
        }

        void CloseConnection()
        {
            if (closeConnection) return;
            closeConnection = true;
            observer.OnCompleted();
        }

        public void OnRequest(HttpRequestHead request, bool shouldKeepAlive)
        {
            var responseDelegate = responseDelegateFactory.Create(request, shouldKeepAlive, CloseConnection);

            DataSubject subject = null;
            bool requestBodyConnected = false;

            Debug.WriteLine("[{0}] {1} {2} shouldKeepAlive = {3}",
                DateTime.Now, request.Method, request.Uri, shouldKeepAlive);

            if (request.HasBody())
            {
                subject = new DataSubject(() =>
                {
                    if (requestBodyConnected) 
                        throw new InvalidOperationException("Request body was already connected.");

                    requestBodyConnected = true;

                    if (request.IsContinueExpected())
                        responseDelegate.WriteContinue();

                    return new Disposable(() =>
                    {
                        // XXX what to do?
                        // ideally we stop reading from the socket. 
                        // equivalent to a parse error
                    });
                });
            }

            requestBody = subject;

            if (remoteAddress != null)
            {
                if (request.Headers.ContainsKey("X-Forwarded-For"))
                {
                    request.Headers["X-Forwarded-For"] += "," + remoteAddress.ToString();
                }
                else
                {
                    request.Headers["X-Forwarded-For"] = remoteAddress.ToString();
                }
            }

            requestDelegate.OnRequest(request, subject, responseDelegate);
            observer.OnNext(responseDelegate);
        }

        public bool OnRequestData(ArraySegment<byte> data, Action continuation)
        {
            return requestBody.OnData(data, continuation);
        }

        public void OnRequestEnd()
        {
            if (requestBody != null)
                requestBody.OnEnd();
        }

        public void OnError(Exception e)
        {
            if (requestBody != null)
                requestBody.OnError(e);
        }

        public void OnEnd()
        {
            CloseConnection();
        }

        public void Dispose()
        {
            // XXX perhaps return self to freelist
        }
    }
}
