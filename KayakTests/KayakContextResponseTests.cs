using System;
using System.Collections.Generic;
using System.Text;
using NUnit.Core;
using NUnit.Framework;
using Moq;
using Kayak;
using System.IO;

namespace KayakTests
{
    // these amount to integration tests and should be re-enabled later.
    //[TestFixture]
    public class KayakContextResponseTests
    {
        string requestString;

        int statusCode;
        string reasonPhrase;
        NameValueDictionary headers;
        string body;

        Mock<ISocket> mockSocket;

        SynchronousMemoryStream stream;
        KayakContext context;

        bool contextCompleted;
        Exception contextException;

        [SetUp]
        public void SetUp()
        {
            stream = new SynchronousMemoryStream();
            requestString = "GET / HTTP/1.0\r\n\r\n";
            stream.Write(Encoding.ASCII.GetBytes(requestString), 0, requestString.Length);
            stream.Position = 0;
            mockSocket = new Mock<ISocket>();
            //mockSocket.Setup(s => s.GetStream()).Returns(stream).Verifiable();
            //context = new KayakContext(mockSocket.Object);
            //context.Subscribe(n => { }, e => contextException = e, () => contextCompleted = true);

            headers = new NameValueDictionary();
            headers["Server"] = "Kayak";
            headers["Date"] = DateTime.UtcNow.ToString();

            body = null;
        }

        void SetUpContext()
        {
            context.Response.StatusCode = statusCode;
            context.Response.ReasonPhrase = reasonPhrase;

            if (headers != null)
                context.Response.Headers = headers;
        }

        void AssertResponse()
        {
            Assert.IsNull(contextException, "Exception was not null.");
            Assert.IsTrue(contextCompleted, "Context did not complete.");

            var expected = string.Format("{0}{1} {2} {3}\r\n", requestString, context.Response.HttpVersion, statusCode, reasonPhrase);

            if (headers != null)
                foreach (var pair in headers)
                    expected += string.Format("{0}: {1}\r\n", pair.Name, pair.Value);

            expected += "\r\n";

            if (body != null)
                expected += body;

            var received = Encoding.UTF8.GetString(stream.ToArray());
            Assert.AreEqual(expected, received, "Unexpected response.");
        }

        [Test]
        public void ResponseDefaultStatusLine()
        {
            statusCode = 200;
            reasonPhrase = "OK";

            context.End();

            AssertResponse();
        }
        [Test]
        public void ResponseStatusLine()
        {
            statusCode = 503;
            reasonPhrase = "Server Blowout";

            SetUpContext();

            context.End();

            AssertResponse();
        }

        [Test]
        public void ResponseWithHeaders()
        {
            statusCode = 302;
            reasonPhrase = "Found";
            headers = new NameValueDictionary();
            headers["Location"] = "http://yo.momma/";
            headers["Server"] = "Yo Daddy";

            SetUpContext();

            context.End();

            AssertResponse();
        }

        [Test]
        public void ResponseWithBody()
        {
            statusCode = 404;
            reasonPhrase = "Not Found";

            body = "Buggeroff.";

            SetUpContext();

            var bodyStream = context.Response.Body;

            bodyStream.Write(Encoding.UTF8.GetBytes(body), 0, body.Length);

            context.End();

            AssertResponse();
        }
    }
}
