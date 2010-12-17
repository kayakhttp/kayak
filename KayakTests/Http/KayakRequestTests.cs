using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Kayak;
using Moq;

namespace KayakTests.Http
{
    [TestFixture]
    class KayakRequestTests
    {
        [Test]
        public void ReadsBodyDataWithOneSocketChunk()
        {
            var mockSocket = Mocks.MockSocket("First chunk");
            var request = new KayakRequest(mockSocket.Object, default(HttpRequestLine), null, null, default(ArraySegment<byte>));

            byte[] buffer = new byte[64];
            var task1 = request.ReadBodyAsync(buffer, 0, buffer.Length);

            Assert.AreEqual("First chunk", Encoding.UTF8.GetString(buffer, 0, task1.Result));

            var task2 = request.ReadBodyAsync(buffer, 0, buffer.Length);

            Assert.AreEqual(0, task2.Result);
        }

        [Test]
        public void ReadsBodyDataWithTwoSocketChunks()
        {
            var chunks = new string[] { "First chunk", "Second chunk" };
            var mockSocket = Mocks.MockSocket(chunks);
            var request = new KayakRequest(mockSocket.Object, default(HttpRequestLine), null, null, default(ArraySegment<byte>));

            byte[] buffer = new byte[64];
            var task1 = request.ReadBodyAsync(buffer, 0, buffer.Length);

            Assert.AreEqual(chunks[0], Encoding.UTF8.GetString(buffer, 0, task1.Result));

            var task2 = request.ReadBodyAsync(buffer, 0, buffer.Length);

            Assert.AreEqual(chunks[1], Encoding.UTF8.GetString(buffer, 0, task2.Result));

            var task3 = request.ReadBodyAsync(buffer, 0, buffer.Length);

            Assert.AreEqual(0, task3.Result);
        }

        [Test]
        public void ReadsBodyDataWithHeaderChunk()
        {
            var headerChunk = "header chunk.";
            var request = new KayakRequest(null, default(HttpRequestLine), null, null, new ArraySegment<byte>(Encoding.UTF8.GetBytes(headerChunk)));

            byte[] buffer = new byte[64];

            var task1 = request.ReadBodyAsync(buffer, 0, buffer.Length);

            Assert.AreEqual(headerChunk, Encoding.UTF8.GetString(buffer, 0, task1.Result));

            var task2 = request.ReadBodyAsync(buffer, 0, buffer.Length);

            Assert.AreEqual(0, task2.Result);
        }

        [Test]
        public void ReadsBodyDataWithHeaderChunkOverMultipleReads()
        {
            var headerChunk = "header chunk.";

            var request = new KayakRequest(null, default(HttpRequestLine), null, null, new ArraySegment<byte>(Encoding.UTF8.GetBytes(headerChunk)));

            byte[] buffer = new byte[10];

            var task1 = request.ReadBodyAsync(buffer, 0, buffer.Length);

            Assert.AreEqual(headerChunk.Substring(0, buffer.Length), Encoding.UTF8.GetString(buffer, 0, task1.Result));

            var task2 = request.ReadBodyAsync(buffer, 0, buffer.Length);

            Assert.AreEqual(headerChunk.Substring(task1.Result, headerChunk.Length - task1.Result), 
                Encoding.UTF8.GetString(buffer, 0, task2.Result));

            var task3 = request.ReadBodyAsync(buffer, 0, buffer.Length);

            Assert.AreEqual(0, task3.Result);
        }

        [Test]
        public void ReadsBodyDataWithHeaderChunkBeforeReadingSocket()
        {
            var headerChunk = "header chunk.";
            var chunks = new string[] { "First chunk", "Second chunk" };
            var mockSocket = Mocks.MockSocket(chunks);

            var request = new KayakRequest(mockSocket.Object, default(HttpRequestLine), null, null, new ArraySegment<byte>(Encoding.UTF8.GetBytes(headerChunk)));

            byte[] buffer = new byte[64];

            var task1 = request.ReadBodyAsync(buffer, 0, buffer.Length);
            Assert.AreEqual(headerChunk, Encoding.UTF8.GetString(buffer, 0, task1.Result));

            var task2 = request.ReadBodyAsync(buffer, 0, buffer.Length);
            Assert.AreEqual(chunks[0], Encoding.UTF8.GetString(buffer, 0, task2.Result));

            var task3 = request.ReadBodyAsync(buffer, 0, buffer.Length);
            Assert.AreEqual(chunks[1], Encoding.UTF8.GetString(buffer, 0, task3.Result));

            var task4 = request.ReadBodyAsync(buffer, 0, buffer.Length);
            Assert.AreEqual(0, task3.Result);
        }
    }
}
