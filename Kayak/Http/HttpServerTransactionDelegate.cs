using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace Kayak.Http
{
    interface IResponse : IDataProducer
    {
        void WriteContinue();
    }

    interface IResponseFactory
    {
        IResponse Create(HttpRequestHead head, IDataProducer body, bool shouldKeepAlive, Action end);
    }

    class ResponseFactory : IResponseFactory
    {
        IHttpRequestDelegate requestDelegate;

        public ResponseFactory(IHttpRequestDelegate requestDelegate)
        {
            this.requestDelegate = requestDelegate;
        }

        public IResponse Create(HttpRequestHead request, IDataProducer body, bool shouldKeepAlive, Action end)
        {
            var responseDelegate = new HttpResponseDelegate(
                prohibitBody: request.Method.ToUpperInvariant() == "HEAD",
                shouldKeepAlive: shouldKeepAlive,
                closeConnection: end);

            requestDelegate.OnRequest(request, body, responseDelegate);

            return responseDelegate;
        }
    }

    class HttpServerTransactionDelegate : IHttpServerTransactionDelegate, IObservable<IDataProducer>
    {
        IResponseFactory responseFactory;
        IObserver<IDataProducer> observer;
        DataSubject subject;

        public HttpServerTransactionDelegate(IResponseFactory responseFactory)
        {
            this.responseFactory = responseFactory;
        }

        public IDisposable Subscribe(IObserver<IDataProducer> observer)
        {
            this.observer = observer;

            return new Disposable(() => OnError(new Exception("Output stream cancelled")));
        }

        void RequestBodyCancelled()
        {
            // XXX really need to think about this behavior more.
            // ideally we stop reading from the socket.

            //observer.OnError(new Exception("Connection was aborted by user."));
        }

        bool closeConnection;

        void CloseConnection()
        {
            if (closeConnection) return;
            closeConnection = true;
            observer.OnCompleted();
        }

        public void OnRequest(HttpRequestHead request, bool shouldKeepAlive)
        {
            IResponse responseConsumer = null;
            bool shouldWriteContinue = false;
            bool requestBodyConnected = false;

            // not very happy with this inside-out state manipulating logic.
            subject = new DataSubject(() => {
                if (requestBodyConnected) throw new InvalidOperationException("Request body was already connected.");
                requestBodyConnected = true;

                if (request.IsContinueExpected())
                {
                    if (responseConsumer == null)
                        shouldWriteContinue = true;
                    else
                        responseConsumer.WriteContinue();
                }
                return new Disposable(RequestBodyCancelled);
            });

            // the subject could be subscribed to and immediately cancelled from within this call!
            responseConsumer = responseFactory.Create(request, subject, shouldKeepAlive, CloseConnection);

            // hate this flag in particular
            if (shouldWriteContinue)
                responseConsumer.WriteContinue();

            observer.OnNext(responseConsumer);
        }

        public bool OnRequestData(ArraySegment<byte> data, Action continuation)
        {
            return subject.OnData(data, continuation);
        }

        public void OnRequestEnd()
        {
            subject.OnEnd();
        }

        public void OnError(Exception e)
        {
            if (subject != null)
                subject.OnError(e);

            observer.OnError(e);
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
