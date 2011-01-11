using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Kayak;
using Moq;
using System.Threading;
using System.Threading.Tasks;

namespace KayakTests.Http
{
    [TestFixture]
    public class KayakRequestTests
    {
        // TODO test when header chunk offset is non-zero

        int result;
        Exception exception;
        Action<int> resultCb;
        Action<Exception> exceptionCb;

        [SetUp]
        public void SetUp()
        {
            result = 0;
            exception = null;
            resultCb = r => result = r;
            exceptionCb = e => exception = e;
        }

        [Test]
        public void ReadsBodyDataWithOneSocketChunk()
        {
            var mockSocket = Mocks.MockSocket("First chunk");

            var request = new HttpSupport().CreateReadBody(mockSocket.Object, default(ArraySegment<byte>));

            byte[] buffer = new byte[64];

            request(buffer, 0, buffer.Length, resultCb, exceptionCb);

            Assert.AreEqual("First chunk", Encoding.UTF8.GetString(buffer, 0, result));

            request(buffer, 0, buffer.Length, r => result = r, e => exception = e);

            Assert.AreEqual(0, result);
        }

        [Test]
        public void ReadsBodyDataWithTwoSocketChunks()
        {
            var chunks = new string[] { "First chunk", "Second chunk" };
            var mockSocket = Mocks.MockSocket(chunks);
            var request = new HttpSupport().CreateReadBody(mockSocket.Object, default(ArraySegment<byte>));

            byte[] buffer = new byte[64];

            request(buffer, 0, buffer.Length, resultCb, exceptionCb);

            Assert.AreEqual(chunks[0], Encoding.UTF8.GetString(buffer, 0, result));

            request(buffer, 0, buffer.Length, resultCb, exceptionCb);

            Assert.AreEqual(chunks[1], Encoding.UTF8.GetString(buffer, 0, result));

            request(buffer, 0, buffer.Length, resultCb, exceptionCb);

            Assert.AreEqual(0, result);
        }

        [Test]
        public void ReadsBodyDataWithHeaderChunk()
        {
            Console.WriteLine("ass");
            var headerChunk = "header chunk.";

            var request = new HttpSupport().CreateReadBody(null, new ArraySegment<byte>(Encoding.UTF8.GetBytes(headerChunk)));
            
            byte[] buffer = new byte[64];

            request(buffer, 0, buffer.Length, resultCb, exceptionCb);

            Assert.AreEqual(headerChunk, Encoding.UTF8.GetString(buffer, 0, result));

            request(buffer, 0, buffer.Length, resultCb, exceptionCb);

            Assert.AreEqual(0, result);
        }

        [Test]
        public void ReadsBodyDataWithHeaderChunkOverMultipleReads()
        {
            var headerChunk = "header chunk.";

            var request = new HttpSupport().CreateReadBody(null, new ArraySegment<byte>(Encoding.UTF8.GetBytes(headerChunk)));

            byte[] buffer = new byte[10];

            request(buffer, 0, buffer.Length, resultCb, exceptionCb);

            Assert.AreEqual(headerChunk.Substring(0, buffer.Length), Encoding.UTF8.GetString(buffer, 0, result));

            int result2 = 0;
            request(buffer, 0, buffer.Length, resultCb, exceptionCb);

            Assert.AreEqual(headerChunk.Substring(result, headerChunk.Length - result), 
                Encoding.UTF8.GetString(buffer, 0, result2));

            request(buffer, 0, buffer.Length, resultCb, exceptionCb);

            Assert.AreEqual(0, result);
        }

        [Test]
        public void ReadsBodyDataWithHeaderChunkBeforeReadingSocket()
        {
            var headerChunk = "header chunk.";
            var chunks = new string[] { "First chunk", "Second chunk" };
            var mockSocket = Mocks.MockSocket(chunks);

            var request = new HttpSupport().CreateReadBody(mockSocket.Object, new ArraySegment<byte>(Encoding.UTF8.GetBytes(headerChunk)));

            byte[] buffer = new byte[64];

            request(buffer, 0, buffer.Length, resultCb, exceptionCb);

            Assert.AreEqual(headerChunk, Encoding.UTF8.GetString(buffer, 0, result));

            request(buffer, 0, buffer.Length, resultCb, exceptionCb);

            Assert.AreEqual(chunks[0], Encoding.UTF8.GetString(buffer, 0, result));

            request(buffer, 0, buffer.Length, resultCb, exceptionCb);

            Assert.AreEqual(chunks[1], Encoding.UTF8.GetString(buffer, 0, result));

            request(buffer, 0, buffer.Length, resultCb, exceptionCb);

            Assert.AreEqual(0, result);
        }
    }
}
