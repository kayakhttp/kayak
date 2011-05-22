using System;
using Kayak;
using Kayak.Http;
using NUnit.Framework;
using System.Collections.Generic;
using System.Text;

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
        public Func<HttpRequestHead, IDataProducer, bool, Action, IDataProducer> OnRequest;

        public IDataProducer Create(HttpRequestHead head, IDataProducer body, bool shouldKeepAlive, Action end)
        {
            return OnRequest(head, body, shouldKeepAlive, end);
        }
    }
}

//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;
//using NUnit.Framework;
//using Kayak.Http;
//using Kayak;

//namespace KayakTests.Http
//{
//    [TestFixture]
//    public class HttpServerTransactionDelegateTests
//    {
//        HttpServerTransactionDelegate txDel;
//        bool shouldKeepAlive;
//        int requestRaised;
//        //Action<IHttpServerRequest, IHttpResponse> onRequest;
//        List<IBufferedOutputStreamDelegate> outputDels;
//        MockSocket socket;
//        List<MockResponse> responses;
//        List<MockBufferedOutputStream> outputStreams;


//        [SetUp]
//        public void SetUp()
//        {
//            shouldKeepAlive = false;
//            requestRaised = 0;
//            responses = new List<MockResponse>();
//            outputStreams = new List<MockBufferedOutputStream>();
//            outputDels = new List<IBufferedOutputStreamDelegate>();
//            onRequest = null;
//            txDel = new HttpServerTransactionDelegate(
//                (req, res) => { requestRaised++; if (onRequest != null) onRequest(req, res); },
//                (req, outputDelegate, _shouldKeepAlive) =>
//                {
//                    Assert.That(outputDelegate, Is.Not.Null);
//                    Assert.That(_shouldKeepAlive, Is.EqualTo(shouldKeepAlive));

//                    outputDels.Add(outputDelegate);

//                    var response = new MockResponse();
//                    responses.Add(response);
//                    var output = new MockBufferedOutputStream();
//                    outputStreams.Add(output);
//                    return Tuple.Create(
//                        (IHttpServerResponseInternal)response, 
//                        (IBufferedOutputStream)output);
//                });
//            socket = new MockSocket();
//        }

//        [Test]
//        public void One_request_drain_after_end()
//        {
//            txDel.OnBegin(socket);

//            Assert.That(requestRaised, Is.EqualTo(0));
//            Assert.That(responses.Count, Is.EqualTo(0));
//            Assert.That(outputStreams.Count, Is.EqualTo(0));
//            shouldKeepAlive = false;

//            txDel.OnRequest(null, false);

//            Assert.That(requestRaised, Is.EqualTo(1));
//            Assert.That(responses.Count, Is.EqualTo(1));
//            Assert.That(outputStreams.Count, Is.EqualTo(1));
//            Assert.That(outputStreams[0].WasAttached, Is.True);

//            responses[0].KeepAlive = false;

//            txDel.OnEnd();

//            Assert.That(socket.GotEnd, Is.False);

//            outputDels[0].OnDrained(outputStreams[0]);

//            Assert.That(socket.GotEnd, Is.True);
//        }

//        [Test]
//        public void One_request_drain_on_request()
//        {
//            txDel.OnBegin(socket);

//            Assert.That(requestRaised, Is.EqualTo(0));
//            Assert.That(responses.Count, Is.EqualTo(0));
//            Assert.That(outputStreams.Count, Is.EqualTo(0));
//            shouldKeepAlive = true;

//            onRequest = (req, res) =>
//            {
//                Assert.That(responses.Count, Is.EqualTo(1));
//                responses[0].KeepAlive = false;

//                Assert.That(outputStreams.Count, Is.EqualTo(1));
//                Assert.That(outputStreams[0].WasAttached, Is.True);
//                Assert.That(socket.GotEnd, Is.False);

//                outputDels[0].OnDrained(outputStreams[0]);

//                Assert.That(socket.GotEnd, Is.True);
//            };

//            txDel.OnRequest(null, true);

//            Assert.That(requestRaised, Is.EqualTo(1));
//        }

//        [Test]
//        public void Two_requests_drain_after_end()
//        {
//            txDel.OnBegin(socket);

//            Assert.That(requestRaised, Is.EqualTo(0));
//            Assert.That(responses.Count, Is.EqualTo(0));
//            Assert.That(outputStreams.Count, Is.EqualTo(0));
//            shouldKeepAlive = true;

//            txDel.OnRequest(null, true);

//            Assert.That(requestRaised, Is.EqualTo(1));
//            Assert.That(responses.Count, Is.EqualTo(1));
//            Assert.That(outputStreams.Count, Is.EqualTo(1));
//            Assert.That(outputStreams[0].WasAttached, Is.True);

//            shouldKeepAlive = false;

//            txDel.OnRequest(null, false);

//            Assert.That(requestRaised, Is.EqualTo(2));
//            Assert.That(responses.Count, Is.EqualTo(2));
//            Assert.That(outputStreams.Count, Is.EqualTo(2));

//            txDel.OnEnd();

//            responses[0].KeepAlive = true;

//            Assert.That(outputStreams[1].WasAttached, Is.False);

//            outputDels[0].OnDrained(outputStreams[0]);

//            Assert.That(outputStreams[1].WasAttached, Is.True);
//            Assert.That(socket.GotEnd, Is.False);

//            responses[1].KeepAlive = false;
//            outputDels[1].OnDrained(outputStreams[1]);

//            Assert.That(socket.GotEnd, Is.True);
//        }

//        [Test]
//        public void Two_requests_drain_on_request()
//        {
//            txDel.OnBegin(socket);

//            Assert.That(requestRaised, Is.EqualTo(0));
//            Assert.That(responses.Count, Is.EqualTo(0));
//            Assert.That(outputStreams.Count, Is.EqualTo(0));
//            shouldKeepAlive = true;

//            onRequest = (req, res) =>
//            {
//                Assert.That(responses.Count, Is.EqualTo(1));
//                responses[0].KeepAlive = true;

//                Assert.That(outputStreams.Count, Is.EqualTo(1));
//                Assert.That(outputStreams[0].WasAttached, Is.True);

//                outputDels[0].OnDrained(outputStreams[0]);

//                Assert.That(socket.GotEnd, Is.False);
//            };

//            txDel.OnRequest(null, true);

//            Assert.That(requestRaised, Is.EqualTo(1));

//            shouldKeepAlive = false;

//            onRequest = (req, res) =>
//            {
//                Assert.That(responses.Count, Is.EqualTo(2));
//                responses[0].KeepAlive = false;

//                Assert.That(outputStreams.Count, Is.EqualTo(2));
//                Assert.That(outputStreams[1].WasAttached, Is.True);
//                Assert.That(socket.GotEnd, Is.False);

//                outputDels[1].OnDrained(outputStreams[1]);

//                Assert.That(socket.GotEnd, Is.True);
//            };

//            txDel.OnRequest(null, false);

//            Assert.That(requestRaised, Is.EqualTo(2));
//        }

//        class MockBufferedOutputStream : IBufferedOutputStream
//        {
//            public bool WasAttached { get; private set; }

//            public void Attach(Kayak.ISocket socket)
//            {
//                if (WasAttached)
//                    throw new Exception("previously attached.");

//                WasAttached = true;
//            }

//            public bool Write(ArraySegment<byte> data, Action continuation)
//            {
//                throw new NotImplementedException();
//            }

//            public void End()
//            {
//                throw new NotImplementedException();
//            }
//        }

//        class MockResponse : IHttpServerResponseInternal
//        {
//            public bool KeepAlive { get; set; }

//            public void WriteContinue()
//            {
//                throw new NotImplementedException();
//            }

//            public void WriteHeaders(HttpResponseHead head)
//            {
//                throw new NotImplementedException();
//            }

//            public bool WriteBody(ArraySegment<byte> data, Action continuation)
//            {
//                throw new NotImplementedException();
//            }

//            public void End()
//            {
//                throw new NotImplementedException();
//            }
//        }

//        class MockSocket : ISocket
//        {
//            public bool GotEnd { get; private set; }

//            public System.Net.IPEndPoint RemoteEndPoint
//            {
//                get { throw new NotImplementedException(); }
//            }

//            public void Connect(System.Net.IPEndPoint ep)
//            {
//                throw new NotImplementedException();
//            }

//            public bool Write(ArraySegment<byte> data, Action continuation)
//            {
//                throw new NotImplementedException();
//            }

//            public void End()
//            {
//                if (GotEnd)
//                    throw new Exception("previously ended.");

//                GotEnd = true;
//            }

//            public void Dispose()
//            {
//                throw new NotImplementedException();
//            }
//        }
//    }
//}
