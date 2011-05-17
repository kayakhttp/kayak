using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace Kayak.Http
{
    interface IResponseFactory
    {
        IDataProducer Create(HttpRequestHead head, IDataProducer body, bool shouldKeepAlive, Action end);
    }

    class ResponseFactory : IResponseFactory
    {
        IHttpChannel channel;

        public ResponseFactory(IHttpChannel channel)
        {
            this.channel = channel;
        }

        public IDataProducer Create(HttpRequestHead request, IDataProducer body, bool shouldKeepAlive, Action end)
        {
            var del = new HttpResponseDelegate(
                prohibitBody: request.Method.ToUpperInvariant() == "HEAD",
                shouldKeepAlive: shouldKeepAlive,
                expectContinue: request.IsContinueExpected(),
                closeConnection: end);

            channel.OnRequest(request, body, del);

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
            observer.OnError(new Exception("Connection was aborted by user."));
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
            subject = new DataSubject(() => new Disposable(RequestBodyCancelled));

            var producer = responseFactory.Create(request, subject, shouldKeepAlive, CloseConnection);

            observer.OnNext(producer);
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
            subject.OnError(e);
            observer.OnError(e);
        }

        public void OnEnd()
        {
            CloseConnection();
        }

        public void OnClose()
        {
            // XXX perhaps return self to freelist
        }
    }
}
