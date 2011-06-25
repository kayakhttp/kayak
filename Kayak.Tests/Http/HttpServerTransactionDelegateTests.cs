using System;
using System.Collections.Generic;
using System.Text;
using Kayak.Http;
using NUnit.Framework;

namespace Kayak.Tests.Http
{
    [TestFixture]
    public class HttpServerTransactionDelegateTests
    {
        MockResponseFactory responseFactory;
        HttpServerTransactionDelegate txDel;
        MockObserver<IDataProducer> outgoingMessageObserver;
        

        [SetUp]
        public void SetUp()
        {
            responseFactory = new MockResponseFactory();
            txDel = new HttpServerTransactionDelegate(responseFactory);
            outgoingMessageObserver = new MockObserver<IDataProducer>();
            txDel.Subscribe(outgoingMessageObserver);
        }

        [Test]
        public void connect_on_request__end_on_request()
        {
            MockDataConsumer requestConsumer = new MockDataConsumer();
            IDisposable requestAbort = null;
            responseFactory.OnRequest = (head, body, keepAlive, end) =>
            {
                requestAbort = body.Connect(requestConsumer);
                end();
                return null;
            };

            txDel.OnRequest(default(HttpRequestHead), false);
            txDel.OnRequestData(new ArraySegment<byte>(Encoding.ASCII.GetBytes("some body")), null);
            txDel.OnRequestEnd();

            Assert.That(outgoingMessageObserver.Values.Count, Is.EqualTo(1));
            Assert.That(outgoingMessageObserver.Values[0], Is.Null);
            Assert.That(outgoingMessageObserver.GotCompleted, Is.True);
            Assert.That(requestConsumer.Buffer.ToString(), Is.EqualTo("some body"));
            Assert.That(requestConsumer.GotEnd, Is.True);
            Assert.That(outgoingMessageObserver.Error, Is.Null);
        }

        [Test]
        public void connect_on_request__end_on_data()
        {
            MockDataConsumer requestConsumer = new MockDataConsumer();
            IDisposable requestAbort = null;
            Action endAction = null;
            requestConsumer.OnDataAction = data => endAction();
            responseFactory.OnRequest = (head, body, keepAlive, end) =>
            {
                requestAbort = body.Connect(requestConsumer);
                endAction = end;
                return null;
            };

            txDel.OnRequest(default(HttpRequestHead), false);
            txDel.OnRequestData(new ArraySegment<byte>(Encoding.ASCII.GetBytes("some body")), null);
            txDel.OnRequestEnd();

            Assert.That(outgoingMessageObserver.Values.Count, Is.EqualTo(1));
            Assert.That(outgoingMessageObserver.Values[0], Is.Null);
            Assert.That(outgoingMessageObserver.GotCompleted, Is.True);
            Assert.That(requestConsumer.Buffer.ToString(), Is.EqualTo("some body"));
            Assert.That(requestConsumer.GotEnd, Is.True);
            Assert.That(outgoingMessageObserver.Error, Is.Null);
        }

        [Test]
        public void connect_on_request__end_on_end()
        {
            MockDataConsumer requestConsumer = new MockDataConsumer();
            IDisposable requestAbort = null;
            Action endAction = null;
            requestConsumer.OnEndAction = () => endAction();
            responseFactory.OnRequest = (head, body, keepAlive, end) =>
            {
                requestAbort = body.Connect(requestConsumer);
                endAction = end;
                return null;
            };

            txDel.OnRequest(default(HttpRequestHead), false);
            txDel.OnRequestData(new ArraySegment<byte>(Encoding.ASCII.GetBytes("some body")), null);
            txDel.OnRequestEnd();

            Assert.That(outgoingMessageObserver.Values.Count, Is.EqualTo(1));
            Assert.That(outgoingMessageObserver.Values[0], Is.Null);
            Assert.That(outgoingMessageObserver.GotCompleted, Is.True);
            Assert.That(requestConsumer.Buffer.ToString(), Is.EqualTo("some body"));
            Assert.That(requestConsumer.GotEnd, Is.True);
            Assert.That(outgoingMessageObserver.Error, Is.Null);
        }

        [Test]
        public void connect_after_on_request__end_on_request()
        {
            MockDataConsumer requestConsumer = new MockDataConsumer();
            IDisposable requestAbort = null;
            IDataProducer bodyProducer = null;
            responseFactory.OnRequest = (head, body, keepAlive, end) =>
            {
                end();
                bodyProducer = body;
                return null;
            };

            txDel.OnRequest(default(HttpRequestHead), false);

            requestAbort = bodyProducer.Connect(requestConsumer);

            txDel.OnRequestData(new ArraySegment<byte>(Encoding.ASCII.GetBytes("some body")), null);
            txDel.OnRequestEnd();

            Assert.That(outgoingMessageObserver.Values.Count, Is.EqualTo(1));
            Assert.That(outgoingMessageObserver.Values[0], Is.Null);
            Assert.That(outgoingMessageObserver.GotCompleted, Is.True);
            Assert.That(requestConsumer.Buffer.ToString(), Is.EqualTo("some body"));
            Assert.That(requestConsumer.GotEnd, Is.True);
            Assert.That(outgoingMessageObserver.Error, Is.Null);
        }

        [Test]
        public void connect_afer_on_request__end_on_data()
        {
            MockDataConsumer requestConsumer = new MockDataConsumer();
            IDisposable requestAbort = null;
            IDataProducer bodyProducer = null;
            Action endAction = null;
            requestConsumer.OnDataAction = data => endAction();
            responseFactory.OnRequest = (head, body, keepAlive, end) =>
            {
                requestAbort = body.Connect(requestConsumer);
                endAction = end;
                bodyProducer = body;
                return null;
            };

            txDel.OnRequest(default(HttpRequestHead), false);

            requestAbort = bodyProducer.Connect(requestConsumer);

            txDel.OnRequestData(new ArraySegment<byte>(Encoding.ASCII.GetBytes("some body")), null);
            txDel.OnRequestEnd();

            Assert.That(outgoingMessageObserver.Values.Count, Is.EqualTo(1));
            Assert.That(outgoingMessageObserver.Values[0], Is.Null);
            Assert.That(outgoingMessageObserver.GotCompleted, Is.True);
            Assert.That(requestConsumer.Buffer.ToString(), Is.EqualTo("some body"));
            Assert.That(requestConsumer.GotEnd, Is.True);
            Assert.That(outgoingMessageObserver.Error, Is.Null);
        }

        [Test]
        public void connect_after_on_request__end_on_end()
        {
            MockDataConsumer requestConsumer = new MockDataConsumer();
            IDisposable requestAbort = null;
            IDataProducer bodyProducer = null;
            Action endAction = null;
            requestConsumer.OnEndAction = () => endAction();
            responseFactory.OnRequest = (head, body, keepAlive, end) =>
            {
                requestAbort = body.Connect(requestConsumer);
                endAction = end;
                bodyProducer = body;
                return null;
            };

            txDel.OnRequest(default(HttpRequestHead), false);

            requestAbort = bodyProducer.Connect(requestConsumer);

            txDel.OnRequestData(new ArraySegment<byte>(Encoding.ASCII.GetBytes("some body")), null);
            txDel.OnRequestEnd();

            Assert.That(outgoingMessageObserver.Values.Count, Is.EqualTo(1));
            Assert.That(outgoingMessageObserver.Values[0], Is.Null);
            Assert.That(outgoingMessageObserver.GotCompleted, Is.True);
            Assert.That(requestConsumer.Buffer.ToString(), Is.EqualTo("some body"));
            Assert.That(requestConsumer.GotEnd, Is.True);
            Assert.That(outgoingMessageObserver.Error, Is.Null);
        }
    }

    class MockObserver<T> : IObserver<T>
    {
        public bool GotCompleted;
        public Exception Error;
        public List<T> Values = new List<T>();

        public void OnCompleted()
        {
            if (GotCompleted)
                throw new InvalidOperationException("Got completed already.");

            GotCompleted = true;
        }

        public void OnError(Exception error)
        {
            if (error == null)
                throw new ArgumentNullException("error");

            if (Error != null)
                throw new Exception("Already got error");

            Error = error;
        }

        public void OnNext(T value)
        {
            Values.Add(value);
        }
    }


    class MockResponseFactory : IResponseFactory
    {
        public Func<HttpRequestHead, IDataProducer, bool, Action, IResponse> OnRequest;

        public IResponse Create(HttpRequestHead head, IDataProducer body, bool shouldKeepAlive, Action end)
        {
            return OnRequest(head, body, shouldKeepAlive, end);
        }
    }
}
