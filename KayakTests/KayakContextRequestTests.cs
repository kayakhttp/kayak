using System;
using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using System.IO;
using Kayak;
using Moq;
using System.Net.Sockets;
using System.Threading;

namespace KayakTests
{
    // these amount to integration tests and should be re-enabled later.
    //[TestFixture]
    public class KayakContextRequestTests
    {
        Mock<ISocket> mockSocket;

        string verb, requestUri, httpVersion;
        Dictionary<string, string> headers;
        string body;

        KayakContext context;

        bool contextGeneratedUnit;
        Exception contextException;

        IDisposable cx;

        [SetUp]
        public void SetUp()
        {
            mockSocket = null;
            verb = requestUri = httpVersion = body = null;
            headers = null;
            context = null;
            contextGeneratedUnit = false;
            contextException = null;
        }

        void SetUpContext()
        {
            var requestData = string.Format("{0} {1} {2}\r\n", verb, requestUri, httpVersion);

            if (headers != null)
                foreach (var pair in headers)
                    requestData += string.Format("{0}: {1}\r\n", pair.Key, pair.Value);

            requestData += "\r\n";

            if (body != null)
                requestData += body;
             

            Console.WriteLine("Sending request with data:\r\n" + requestData);
            var requestStream = new SynchronousMemoryStream(Encoding.UTF8.GetBytes(requestData));
            mockSocket = new Mock<ISocket>();
            //mockSocket.Setup(s => s.GetStream()).Returns(requestStream).Verifiable();

            //context = new KayakContext(mockSocket.Object);

            //var cx = context.Subscribe(u => contextGeneratedUnit = true, e => contextException = e);
        }

        void AssertRequest()
        {
            cx = null;

            mockSocket.Verify();

            if (contextException != null)
                throw new Exception("Context generated error. " + contextException.Message + "\n" + contextException.StackTrace, contextException);

            Assert.IsTrue(contextGeneratedUnit, "Context did not parse headers.");
            Assert.AreEqual(verb, context.Request.Verb, "Unexpected verb.");
            Assert.AreEqual(requestUri, context.Request.RequestUri, "Unexpected request URI.");
            Assert.AreEqual(httpVersion, context.Request.HttpVersion, "Unexpected HTTP version.");

            if (headers != null)
                foreach (var pair in headers)
                {
                    Assert.IsTrue(context.Request.Headers.ContainsKey(pair.Key), "Parsed headers did not contain name '" + pair.Key + "'");
                    Assert.AreEqual(pair.Value, context.Request.Headers[pair.Key], "Parsed headers contained unexpected value.");
                }
            else
                Assert.AreEqual(0, context.Request.Headers.Count, "Expected no headers.");

            if (body != null)
            {
                Assert.Fail("These tests probably need to be rewritten");
                //Assert.AreEqual(body, new StreamReader(context.Request.Body).ReadToEnd());
            }

        }

        [Test]
        public void ParseStatusLine()
        {
            verb = "GET";
            requestUri = "/foobar";
            httpVersion = "HTTP/1.0";

            SetUpContext();

            AssertRequest();

            //Assert.IsNull(context.Request.Body, "Body was non-null.");
        }

        [Test]
        public void ParseHeaders()
        {
            verb = "GET";
            requestUri = "/foobar";
            httpVersion = "HTTP/1.0";
            headers = new Dictionary<string, string>();
            headers["User-Agent"] = "KayakTests";

            SetUpContext();

            AssertRequest();

            //Assert.IsNull(context.Request.Body, "Body was non-null.");
        }

        [Test]
        public void ParseBodyStream()
        {
            verb = "POST";
            requestUri = "/foobar";
            httpVersion = "HTTP/1.0";
            headers = new Dictionary<string, string>();
            headers["User-Agent"] = "KayakTests";
            body = "Fooooooo. Baaaaarrrrrr.";
            headers["Content-Length"] = body.Length.ToString();

            SetUpContext();

            AssertRequest();
        }
    }
}
