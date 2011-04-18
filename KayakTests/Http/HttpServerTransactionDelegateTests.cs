using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Kayak.Http;
using Kayak;

namespace KayakTests.Http
{
    [TestFixture]
    public class HttpServerTransactionDelegateTests
    {
        HttpServerTransactionDelegate txDel;
        bool shouldKeepAlive;
        int requestRaised;
        Action<IHttpServerRequest, IHttpServerResponse> onRequest;
        List<IBufferedOutputStreamDelegate> outputDels;
        MockSocket socket;
        List<MockResponse> responses;
        List<MockBufferedOutputStream> outputStreams;


        [SetUp]
        public void SetUp()
        {
            shouldKeepAlive = false;
            requestRaised = 0;
            responses = new List<MockResponse>();
            outputStreams = new List<MockBufferedOutputStream>();
            outputDels = new List<IBufferedOutputStreamDelegate>();
            onRequest = null;
            txDel = new HttpServerTransactionDelegate(
                (req, res) => { requestRaised++; if (onRequest != null) onRequest(req, res); },
                (req, outputDelegate, _shouldKeepAlive) =>
                {
                    Assert.That(outputDelegate, Is.Not.Null);
                    Assert.That(_shouldKeepAlive, Is.EqualTo(shouldKeepAlive));

                    outputDels.Add(outputDelegate);

                    var response = new MockResponse();
                    responses.Add(response);
                    var output = new MockBufferedOutputStream();
                    outputStreams.Add(output);
                    return Tuple.Create(
                        (IHttpServerResponseInternal)response, 
                        (IBufferedOutputStream)output);
                });
            socket = new MockSocket();
        }

        [Test]
        public void One_request_drain_after_end()
        {
            txDel.OnBegin(socket);

            Assert.That(requestRaised, Is.EqualTo(0));
            Assert.That(responses.Count, Is.EqualTo(0));
            Assert.That(outputStreams.Count, Is.EqualTo(0));
            shouldKeepAlive = false;

            txDel.OnRequest(null, false);

            Assert.That(requestRaised, Is.EqualTo(1));
            Assert.That(responses.Count, Is.EqualTo(1));
            Assert.That(outputStreams.Count, Is.EqualTo(1));
            Assert.That(outputStreams[0].WasAttached, Is.True);

            responses[0].KeepAlive = false;

            txDel.OnEnd();

            Assert.That(socket.GotEnd, Is.False);

            outputDels[0].OnDrained(outputStreams[0]);

            Assert.That(socket.GotEnd, Is.True);
        }

        [Test]
        public void One_request_drain_on_request()
        {
            txDel.OnBegin(socket);

            Assert.That(requestRaised, Is.EqualTo(0));
            Assert.That(responses.Count, Is.EqualTo(0));
            Assert.That(outputStreams.Count, Is.EqualTo(0));
            shouldKeepAlive = true;

            onRequest = (req, res) =>
            {
                Assert.That(responses.Count, Is.EqualTo(1));
                responses[0].KeepAlive = false;

                Assert.That(outputStreams.Count, Is.EqualTo(1));
                Assert.That(outputStreams[0].WasAttached, Is.True);
                Assert.That(socket.GotEnd, Is.False);

                outputDels[0].OnDrained(outputStreams[0]);

                Assert.That(socket.GotEnd, Is.True);
            };

            txDel.OnRequest(null, true);

            Assert.That(requestRaised, Is.EqualTo(1));
        }

        [Test]
        public void Two_requests_drain_after_end()
        {
            txDel.OnBegin(socket);

            Assert.That(requestRaised, Is.EqualTo(0));
            Assert.That(responses.Count, Is.EqualTo(0));
            Assert.That(outputStreams.Count, Is.EqualTo(0));
            shouldKeepAlive = true;

            txDel.OnRequest(null, true);

            Assert.That(requestRaised, Is.EqualTo(1));
            Assert.That(responses.Count, Is.EqualTo(1));
            Assert.That(outputStreams.Count, Is.EqualTo(1));
            Assert.That(outputStreams[0].WasAttached, Is.True);

            shouldKeepAlive = false;

            txDel.OnRequest(null, false);

            Assert.That(requestRaised, Is.EqualTo(2));
            Assert.That(responses.Count, Is.EqualTo(2));
            Assert.That(outputStreams.Count, Is.EqualTo(2));

            txDel.OnEnd();

            responses[0].KeepAlive = true;

            Assert.That(outputStreams[1].WasAttached, Is.False);

            outputDels[0].OnDrained(outputStreams[0]);

            Assert.That(outputStreams[1].WasAttached, Is.True);
            Assert.That(socket.GotEnd, Is.False);

            responses[1].KeepAlive = false;
            outputDels[1].OnDrained(outputStreams[1]);

            Assert.That(socket.GotEnd, Is.True);
        }

        [Test]
        public void Two_requests_drain_on_request()
        {
            txDel.OnBegin(socket);

            Assert.That(requestRaised, Is.EqualTo(0));
            Assert.That(responses.Count, Is.EqualTo(0));
            Assert.That(outputStreams.Count, Is.EqualTo(0));
            shouldKeepAlive = true;

            onRequest = (req, res) =>
            {
                Assert.That(responses.Count, Is.EqualTo(1));
                responses[0].KeepAlive = true;

                Assert.That(outputStreams.Count, Is.EqualTo(1));
                Assert.That(outputStreams[0].WasAttached, Is.True);

                outputDels[0].OnDrained(outputStreams[0]);

                Assert.That(socket.GotEnd, Is.False);
            };

            txDel.OnRequest(null, true);

            Assert.That(requestRaised, Is.EqualTo(1));

            shouldKeepAlive = false;

            onRequest = (req, res) =>
            {
                Assert.That(responses.Count, Is.EqualTo(2));
                responses[0].KeepAlive = false;

                Assert.That(outputStreams.Count, Is.EqualTo(2));
                Assert.That(outputStreams[1].WasAttached, Is.True);
                Assert.That(socket.GotEnd, Is.False);

                outputDels[1].OnDrained(outputStreams[1]);

                Assert.That(socket.GotEnd, Is.True);
            };

            txDel.OnRequest(null, false);

            Assert.That(requestRaised, Is.EqualTo(2));
        }

        class MockBufferedOutputStream : IBufferedOutputStream
        {
            public bool WasAttached { get; private set; }

            public void Attach(Kayak.ISocket socket)
            {
                if (WasAttached)
                    throw new Exception("previously attached.");

                WasAttached = true;
            }

            public bool Write(ArraySegment<byte> data, Action continuation)
            {
                throw new NotImplementedException();
            }

            public void End()
            {
                throw new NotImplementedException();
            }
        }

        class MockResponse : IHttpServerResponseInternal
        {
            public bool KeepAlive { get; set; }

            public void WriteContinue()
            {
                throw new NotImplementedException();
            }

            public void WriteHeaders(string status, IDictionary<string, string> headers)
            {
                throw new NotImplementedException();
            }

            public bool WriteBody(ArraySegment<byte> data, Action continuation)
            {
                throw new NotImplementedException();
            }

            public void End()
            {
                throw new NotImplementedException();
            }
        }

        class MockSocket : ISocket
        {
            public bool GotEnd { get; private set; }

            public event EventHandler OnConnected;
            public event EventHandler<DataEventArgs> OnData;
            public event EventHandler OnEnd;
            public event EventHandler<ExceptionEventArgs> OnError;
            public event EventHandler OnClose;

            public System.Net.IPEndPoint RemoteEndPoint
            {
                get { throw new NotImplementedException(); }
            }

            public void Connect(System.Net.IPEndPoint ep)
            {
                throw new NotImplementedException();
            }

            public bool Write(ArraySegment<byte> data, Action continuation)
            {
                throw new NotImplementedException();
            }

            public void End()
            {
                if (GotEnd)
                    throw new Exception("previously ended.");

                GotEnd = true;
            }

            public void Dispose()
            {
                throw new NotImplementedException();
            }
        }
    }
}
