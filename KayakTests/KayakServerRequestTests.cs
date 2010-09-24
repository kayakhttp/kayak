using System;
using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using Kayak;
using System.IO;
using System.Linq;
using Moq;

namespace KayakTests
{
    public class KayakServerRequestTests
    {
        [TestFixture]
        public class BeginTests
        {
            List<ArraySegment<byte>> chunks;

            Mock<ISocket> mockSocket;
            Mock<IObserver<Unit>> mockObserver;
            IKayakServerRequest request;

            string verb, requestUri, httpVersion;
            Dictionary<string, string> headers;

            [SetUp]
            public void SetUp()
            {
                chunks = new List<ArraySegment<byte>>();
                mockSocket = Mocks.MockSocketRead(chunks);
                mockObserver = new Mock<IObserver<Unit>>();
                mockObserver.Setup(o => o.OnError(It.IsAny<Exception>())).Callback<Exception>(e =>
                {
                    Console.WriteLine("Observer got exception.");
                    Console.Out.WriteException(e);
                });
                request = new KayakServerRequest(mockSocket.Object);
                headers = new Dictionary<string, string>();
            }

            void AddChunk(string s)
            {
                chunks.Add(new ArraySegment<byte>(Encoding.UTF8.GetBytes(s)));
            }

            void VerifyComplete()
            {
                mockObserver.Verify(o => o.OnError(It.IsAny<Exception>()), Times.Never(), "Unexpected OnError.");
                mockObserver.Verify(o => o.OnNext(It.IsAny<Unit>()), Times.Never(), "Unexpected OnNext.");
                mockObserver.Verify(o => o.OnCompleted(), Times.Once(), "Expected OnCompleted once.");
            }

            void AssertRequestLine()
            {
                Assert.AreEqual(verb, request.Verb, "Unexpected verb.");
                Assert.AreEqual(requestUri, request.RequestUri, "Unexpected request URI.");
                Assert.AreEqual(httpVersion, request.HttpVersion, "Unexpected HTTP version.");
            }

            void AssertHeaders()
            {

            }

            string GetRequestLine()
            {
                return verb + " " + requestUri + " " + httpVersion + "\r\n";
            }

            [Test]
            public void Complete1()
            {
                verb = "GET";
                requestUri = "/";
                httpVersion = "HTTP/1.0";

                AddChunk(GetRequestLine());

                request.Begin().Subscribe(mockObserver.Object);

                VerifyComplete();
                AssertRequestLine();
            }

            [Test]
            public void Complete2()
            {
                verb = "GET";
                requestUri = "/";
                httpVersion = "HTTP/1.0";

                AddChunk(GetRequestLine() + "\r\n\r\n");

                request.Begin().Subscribe(mockObserver.Object);

                VerifyComplete();
                AssertRequestLine();
            }
        }
    }
}
