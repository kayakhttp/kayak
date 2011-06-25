﻿using System;
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
            var del = new HttpResponseDelegate(
                prohibitBody: request.Method.ToUpperInvariant() == "HEAD",
                shouldKeepAlive: shouldKeepAlive,
                closeConnection: end);

            requestDelegate.OnRequest(request, body, del);

            return del;
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
            IResponse response = null;

            subject = new DataSubject(() => {
                response.WriteContinue();
                return new Disposable(RequestBodyCancelled);
            });

            // the subject could be subscribed to and immediately cancelled from within this call!
            response = responseFactory.Create(request, subject, shouldKeepAlive, CloseConnection);

            observer.OnNext(response);
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
