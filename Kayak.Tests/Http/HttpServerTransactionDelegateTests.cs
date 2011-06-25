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
        HttpServerTransactionDelegate txDel;
        MockResponseFactory responseFactory;
        MockResponseDelegate responseDelegate;
        MockObserver<IDataProducer> outgoingMessageObserver;

        HttpRequestHead CreateHead(bool expectContinue)
        {
            var head = default(HttpRequestHead);

            if (expectContinue)
            {
                head.Version = new Version(1, 1);
                head.Headers = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase) { { "Expect", "100-continue" } };
            }

            return head;
        }

        void AssertResponseDelegate(bool expectContinue)
        {
            if (expectContinue)
                Assert.That(responseDelegate.GotWriteContinue, Is.True);
            else
                Assert.That(responseDelegate.GotWriteContinue, Is.Not.True);
        }

        [SetUp]
        public void SetUp()
        {
            responseFactory = new MockResponseFactory();
            responseDelegate = new MockResponseDelegate();
            txDel = new HttpServerTransactionDelegate(responseFactory);
            outgoingMessageObserver = new MockObserver<IDataProducer>();
            txDel.Subscribe(outgoingMessageObserver);
        }

        [Test]
        public void connect_on_request__end_on_request([Values(true, false)] bool expectContinue)
        {
            MockDataConsumer requestConsumer = new MockDataConsumer();
            IDisposable requestAbort = null;
            responseFactory.OnRequest = (head, body, keepAlive, end) =>
            {
                requestAbort = body.Connect(requestConsumer);
                end();
                return responseDelegate;
            };

            txDel.OnRequest(CreateHead(expectContinue), false);
            txDel.OnRequestData(new ArraySegment<byte>(Encoding.ASCII.GetBytes("some body")), null);
            txDel.OnRequestEnd();

            Assert.That(outgoingMessageObserver.Values.Count, Is.EqualTo(1));
            Assert.That(outgoingMessageObserver.Values[0], Is.SameAs(responseDelegate));
            Assert.That(outgoingMessageObserver.GotCompleted, Is.True);
            Assert.That(requestConsumer.Buffer.ToString(), Is.EqualTo("some body"));
            Assert.That(requestConsumer.GotEnd, Is.True);
            Assert.That(outgoingMessageObserver.Error, Is.Null);
            AssertResponseDelegate(expectContinue);
        }

        [Test]
        public void connect_on_request__end_on_data([Values(true, false)] bool expectContinue)
        {
            MockDataConsumer requestConsumer = new MockDataConsumer();
            IDisposable requestAbort = null;
            Action endAction = null;
            requestConsumer.OnDataAction = data => endAction();
            responseFactory.OnRequest = (head, body, keepAlive, end) =>
            {
                requestAbort = body.Connect(requestConsumer);
                endAction = end;
                return responseDelegate;
            };

            txDel.OnRequest(CreateHead(expectContinue), false);
            txDel.OnRequestData(new ArraySegment<byte>(Encoding.ASCII.GetBytes("some body")), null);
            txDel.OnRequestEnd();

            Assert.That(outgoingMessageObserver.Values.Count, Is.EqualTo(1));
            Assert.That(outgoingMessageObserver.Values[0], Is.SameAs(responseDelegate));
            Assert.That(outgoingMessageObserver.GotCompleted, Is.True);
            Assert.That(requestConsumer.Buffer.ToString(), Is.EqualTo("some body"));
            Assert.That(requestConsumer.GotEnd, Is.True);
            Assert.That(outgoingMessageObserver.Error, Is.Null);
            AssertResponseDelegate(expectContinue);
        }

        [Test]
        public void connect_on_request__end_on_end([Values(true, false)] bool expectContinue)
        {
            MockDataConsumer requestConsumer = new MockDataConsumer();
            IDisposable requestAbort = null;
            Action endAction = null;
            requestConsumer.OnEndAction = () => endAction();
            responseFactory.OnRequest = (head, body, keepAlive, end) =>
            {
                requestAbort = body.Connect(requestConsumer);
                endAction = end;
                return responseDelegate;
            };

            txDel.OnRequest(CreateHead(expectContinue), false);
            txDel.OnRequestData(new ArraySegment<byte>(Encoding.ASCII.GetBytes("some body")), null);
            txDel.OnRequestEnd();

            Assert.That(outgoingMessageObserver.Values.Count, Is.EqualTo(1));
            Assert.That(outgoingMessageObserver.Values[0], Is.SameAs(responseDelegate));
            Assert.That(outgoingMessageObserver.GotCompleted, Is.True);
            Assert.That(requestConsumer.Buffer.ToString(), Is.EqualTo("some body"));
            Assert.That(requestConsumer.GotEnd, Is.True);
            Assert.That(outgoingMessageObserver.Error, Is.Null);
            AssertResponseDelegate(expectContinue);
        }

        [Test]
        public void connect_after_on_request__end_on_request([Values(true, false)] bool expectContinue)
        {
            MockDataConsumer requestConsumer = new MockDataConsumer();
            IDisposable requestAbort = null;
            IDataProducer bodyProducer = null;
            responseFactory.OnRequest = (head, body, keepAlive, end) =>
            {
                end();
                bodyProducer = body;
                return responseDelegate;
            };

            txDel.OnRequest(CreateHead(expectContinue), false);

            requestAbort = bodyProducer.Connect(requestConsumer);

            txDel.OnRequestData(new ArraySegment<byte>(Encoding.ASCII.GetBytes("some body")), null);
            txDel.OnRequestEnd();

            Assert.That(outgoingMessageObserver.Values.Count, Is.EqualTo(1));
            Assert.That(outgoingMessageObserver.Values[0], Is.SameAs(responseDelegate));
            Assert.That(outgoingMessageObserver.GotCompleted, Is.True);
            Assert.That(requestConsumer.Buffer.ToString(), Is.EqualTo("some body"));
            Assert.That(requestConsumer.GotEnd, Is.True);
            Assert.That(outgoingMessageObserver.Error, Is.Null);
            AssertResponseDelegate(expectContinue);
        }

        [Test]
        public void connect_after_on_request__end_on_data([Values(true, false)] bool expectContinue)
        {
            MockDataConsumer requestConsumer = new MockDataConsumer();
            IDisposable requestAbort = null;
            IDataProducer bodyProducer = null;
            Action endAction = null;
            requestConsumer.OnDataAction = data => endAction();
            responseFactory.OnRequest = (head, body, keepAlive, end) =>
            {
                endAction = end;
                bodyProducer = body;
                return responseDelegate;
            };

            txDel.OnRequest(CreateHead(expectContinue), false);

            requestAbort = bodyProducer.Connect(requestConsumer);

            txDel.OnRequestData(new ArraySegment<byte>(Encoding.ASCII.GetBytes("some body")), null);
            txDel.OnRequestEnd();

            Assert.That(outgoingMessageObserver.Values.Count, Is.EqualTo(1));
            Assert.That(outgoingMessageObserver.Values[0], Is.SameAs(responseDelegate));
            Assert.That(outgoingMessageObserver.GotCompleted, Is.True);
            Assert.That(requestConsumer.Buffer.ToString(), Is.EqualTo("some body"));
            Assert.That(requestConsumer.GotEnd, Is.True);
            Assert.That(outgoingMessageObserver.Error, Is.Null);
            AssertResponseDelegate(expectContinue);
        }

        [Test]
        public void connect_after_on_request__end_on_end([Values(true, false)] bool expectContinue)
        {
            MockDataConsumer requestConsumer = new MockDataConsumer();
            IDisposable requestAbort = null;
            IDataProducer bodyProducer = null;
            Action endAction = null;
            requestConsumer.OnEndAction = () => endAction();
            responseFactory.OnRequest = (head, body, keepAlive, end) =>
            {
                endAction = end;
                bodyProducer = body;
                return responseDelegate;
            };

            txDel.OnRequest(CreateHead(expectContinue), false);

            requestAbort = bodyProducer.Connect(requestConsumer);

            txDel.OnRequestData(new ArraySegment<byte>(Encoding.ASCII.GetBytes("some body")), null);
            txDel.OnRequestEnd();

            Assert.That(outgoingMessageObserver.Values.Count, Is.EqualTo(1));
            Assert.That(outgoingMessageObserver.Values[0], Is.SameAs(responseDelegate));
            Assert.That(outgoingMessageObserver.GotCompleted, Is.True);
            Assert.That(requestConsumer.Buffer.ToString(), Is.EqualTo("some body"));
            Assert.That(requestConsumer.GotEnd, Is.True);
            Assert.That(outgoingMessageObserver.Error, Is.Null);
            AssertResponseDelegate(expectContinue);
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

    class MockResponseDelegate : IResponse
    {
        public bool GotWriteContinue;

        public void WriteContinue()
        {
            if (GotWriteContinue)
                throw new Exception("Already got WriteContinue()");

            GotWriteContinue = true;
        }

        public IDisposable Connect(IDataConsumer channel)
        {
            throw new NotImplementedException();
        }
    }
}
