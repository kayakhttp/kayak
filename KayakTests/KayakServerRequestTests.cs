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
    //public class KayakServerRequestTests
    //{
    //    [TestFixture]
    //    public class BeginTests
    //    {
    //        List<ArraySegment<byte>> chunks, readChunks;

    //        Mock<ISocket> mockSocket;
    //        Mock<IObserver<Unit>> mockBeginObserver;
    //        Mock<IObserver<ArraySegment<byte>>> mockReadObserver;
    //        IKayakServerRequest request;

    //        string verb, requestUri, httpVersion;
    //        Dictionary<string, string> headers;

    //        [SetUp]
    //        public void SetUp()
    //        {
    //            chunks = new List<ArraySegment<byte>>();
    //            readChunks = new List<ArraySegment<byte>>();
    //            mockSocket = Mocks.MockSocketRead(chunks);
    //            mockBeginObserver = new Mock<IObserver<Unit>>();
    //            mockBeginObserver.Setup(o => o.OnError(It.IsAny<Exception>())).Callback<Exception>(e =>
    //            {
    //                Console.WriteLine("Begin Observer got exception.");
    //                Console.Out.WriteException(e);
    //            });

    //            mockReadObserver = new Mock<IObserver<ArraySegment<byte>>>();
    //            mockReadObserver.Setup(o => o.OnError(It.IsAny<Exception>())).Callback<Exception>(e =>
    //            {
    //                Console.WriteLine("Read Observer got exception.");
    //                Console.Out.WriteException(e);
    //            });
    //            mockReadObserver.Setup(o => o.OnNext(It.IsAny<ArraySegment<byte>>())).Callback<ArraySegment<byte>>(d =>
    //            {
    //                readChunks.Add(d);
    //            });

    //            request = new KayakServerRequest(mockSocket.Object);
    //            headers = new Dictionary<string, string>();
    //        }

    //        void AddChunk(string s)
    //        {
    //            chunks.Add(new ArraySegment<byte>(Encoding.UTF8.GetBytes(s)));
    //        }

    //        void VerifyBeginComplete()
    //        {
    //            mockBeginObserver.Verify(o => o.OnError(It.IsAny<Exception>()), Times.Never(), "Unexpected OnError.");
    //            mockBeginObserver.Verify(o => o.OnNext(It.IsAny<Unit>()), Times.Never(), "Unexpected OnNext.");
    //            mockBeginObserver.Verify(o => o.OnCompleted(), Times.Once(), "Expected OnCompleted once.");
    //        }

    //        void AssertRequestLine()
    //        {
    //            Assert.AreEqual(verb, request.Verb, "Unexpected verb.");
    //            Assert.AreEqual(requestUri, request.RequestUri, "Unexpected request URI.");
    //            Assert.AreEqual(httpVersion, request.HttpVersion, "Unexpected HTTP version.");
    //        }

    //        void AssertHeaders()
    //        {
    //            Assert.AreEqual(headers.Count, request.Headers.Count, "Unexpected header count.");

    //            foreach (var pair in headers)
    //            {
    //                Assert.IsTrue(request.Headers.ContainsKey(pair.Key), "Missing header '" + pair.Key + "'.");
    //                Assert.AreEqual(headers[pair.Key], request.Headers[pair.Key], "Unexpected header value.");
    //            }
    //        }

    //        void AssertReads(int times)
    //        {
    //            mockReadObserver.Verify(o => o.OnError(It.IsAny<Exception>()), Times.Never(), "Unexpected error during read.");
    //            mockReadObserver.Verify(o => o.OnNext(It.IsAny<ArraySegment<byte>>()), Times.Exactly(times), "Unexpected OnNext call count.");
    //            mockReadObserver.Verify(o => o.OnCompleted(), Times.Exactly(times), "Unexpected OnCompleted call count.");
    //        }

    //        string GetRequestLine()
    //        {
    //            return verb + " " + requestUri + " " + httpVersion + "\r\n";
    //        }

    //        string GetHeaders()
    //        {
    //            var sb = new StringBuilder();

    //            foreach (var pair in headers)
    //                sb.AppendFormat("{0}: {1}\r\n", pair.Key, pair.Value);

    //            sb.Append("\r\n");
    //            return sb.ToString();
    //        }


    //        [Test]
    //        public void Begin1()
    //        {
    //            verb = "GET";
    //            requestUri = "/";
    //            httpVersion = "HTTP/1.0";

    //            AddChunk(GetRequestLine());

    //            request.Begin().Subscribe(mockBeginObserver.Object);

    //            VerifyBeginComplete();
    //            AssertRequestLine();
    //            AssertHeaders();
    //        }

    //        [Test]
    //        public void Begin2()
    //        {
    //            verb = "GET";
    //            requestUri = "/";
    //            httpVersion = "HTTP/1.0";

    //            AddChunk(GetRequestLine() + "\r\n");

    //            request.Begin().Subscribe(mockBeginObserver.Object);

    //            VerifyBeginComplete();
    //            AssertRequestLine();
    //            AssertHeaders();
    //        }

    //        [Test]
    //        public void Begin3()
    //        {
    //            verb = "POST";
    //            requestUri = "/asdf";
    //            httpVersion = "HTTP/1.0";
    //            AddChunk(GetRequestLine() + "\r\n" + "asdf");

    //            request.Begin().Subscribe(mockBeginObserver.Object);

    //            VerifyBeginComplete();
    //            AssertRequestLine();
    //            AssertHeaders();
    //        }

    //        [Test]
    //        public void BeginWithHeaders1()
    //        {
    //            verb = "POST";
    //            requestUri = "/asdf";
    //            httpVersion = "HTTP/1.0";

    //            headers["user-agent"] = "tests";

    //            AddChunk(GetRequestLine() + GetHeaders());

    //            request.Begin().Subscribe(mockBeginObserver.Object);

    //            VerifyBeginComplete();
    //            AssertRequestLine();
    //            AssertHeaders();
    //        }

    //        [Test]
    //        public void BeginWithHeaders2()
    //        {
    //            verb = "POST";
    //            requestUri = "/asdf";
    //            httpVersion = "HTTP/1.0";

    //            headers["user-agent"] = "tests";
    //            headers["date"] = "time to get a watch";

    //            AddChunk(GetRequestLine() + GetHeaders());

    //            request.Begin().Subscribe(mockBeginObserver.Object);

    //            VerifyBeginComplete();
    //            AssertRequestLine();
    //            AssertHeaders();
    //        }

    //        [Test]
    //        public void BeginAndRead1()
    //        {
    //            verb = "POST";
    //            requestUri = "/asdf";
    //            httpVersion = "HTTP/1.0";
    //            headers["user-agent"] = "test";
    //            headers["content-type"] = "text/plain";
    //            headers["content-length"] = "4";

    //            AddChunk(GetRequestLine() + GetHeaders());
    //            AddChunk("asdf");

    //            request.Begin().Subscribe(mockBeginObserver.Object);
    //            request.Read().Subscribe(mockReadObserver.Object);

    //            VerifyBeginComplete();
    //            AssertRequestLine();
    //            AssertHeaders();
    //            AssertReads(1);

    //            Assert.AreEqual("asdf", readChunks.GetString(), "Incorrect body data.");
    //        }

    //        [Test]
    //        public void BeginAndRead2()
    //        {
    //            verb = "POST";
    //            requestUri = "/asdf";
    //            httpVersion = "HTTP/1.0";
    //            headers["user-agent"] = "test";
    //            headers["content-type"] = "text/plain";
    //            headers["content-length"] = "4";

    //            AddChunk(GetRequestLine() + GetHeaders() + "asdf");

    //            request.Begin().Subscribe(mockBeginObserver.Object);
    //            request.Read().Subscribe(mockReadObserver.Object);

    //            VerifyBeginComplete();
    //            AssertRequestLine();
    //            AssertHeaders();
    //            AssertReads(1);

    //            Assert.AreEqual("asdf", readChunks.GetString(), "Incorrect body data.");
    //        }

    //        [Test]
    //        public void BeginAndRead3()
    //        {
    //            verb = "POST";
    //            requestUri = "/asdf";
    //            httpVersion = "HTTP/1.0";
    //            headers["user-agent"] = "test";
    //            headers["content-type"] = "text/plain";
    //            headers["content-length"] = "8";

    //            AddChunk(GetRequestLine() + GetHeaders() + "asdf");
    //            AddChunk("adsf");

    //            request.Begin().Subscribe(mockBeginObserver.Object);
    //            request.Read().Subscribe(mockReadObserver.Object);
    //            request.Read().Subscribe(mockReadObserver.Object);

    //            VerifyBeginComplete();
    //            AssertRequestLine();
    //            AssertHeaders();
    //            AssertReads(2);

    //            Assert.AreEqual("asdfadsf", readChunks.GetString(), "Incorrect body data.");
    //        }

    //        [Test]
    //        public void BeginAndReadOnceMore()
    //        {
    //            verb = "POST";
    //            requestUri = "/asdf";
    //            httpVersion = "HTTP/1.0";
    //            headers["user-agent"] = "test";
    //            headers["content-type"] = "text/plain";
    //            headers["content-length"] = "8";

    //            AddChunk(GetRequestLine() + GetHeaders() + "asdf");
    //            AddChunk("adsf");

    //            request.Begin().Subscribe(mockBeginObserver.Object);
    //            request.Read().Subscribe(mockReadObserver.Object);
    //            request.Read().Subscribe(mockReadObserver.Object);
    //            request.Read().Subscribe(mockReadObserver.Object);

    //            VerifyBeginComplete();
    //            AssertRequestLine();
    //            AssertHeaders();
    //            AssertReads(3);

    //            Assert.AreEqual("asdfadsf", readChunks.GetString(), "Incorrect body data.");

    //            Assert.AreEqual(0, readChunks.Last().Count, "Expected to read 0 bytes.");
    //        }
    //    }
    //}
}
