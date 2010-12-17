using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Kayak;
using NUnit.Core;
using NUnit.Framework;

namespace KayakTests
{
    public class BuffersTests
    {
        [TestFixture]
        public class BufferHeadersTests
        {
            string StringAtIndex(IEnumerable<ArraySegment<byte>> chunks, int index)
            {
                var chunk = chunks.ElementAt(index);

                Console.WriteLine("chunk offset = " + chunk.Offset + " count = " + chunk.Count);
                return Encoding.UTF8.GetString(chunk.Array, chunk.Offset, chunk.Count);
            }

            [Test]
            public void OneChunk()
            {
                var chunks = new string[] { "adsfasdf\r\n\r\n" };
                var mockSocket = Mocks.MockSocket(chunks);

                var result = mockSocket.Object.BufferHeaders().Result;

                Assert.AreEqual(2, result.Count);
                Assert.AreEqual(chunks[0], StringAtIndex(result, 0));
                Assert.AreEqual(0, result.ElementAt(1).Count);
            }

            [Test]
            public void TwoChunks()
            {
                var chunks = new string[] { "adsf", "asdf\r\n\r\n" };
                var mockSocket = Mocks.MockSocket(chunks);

                var result = mockSocket.Object.BufferHeaders().Result;

                Assert.AreEqual(3, result.Count);
                Assert.AreEqual(chunks[0], StringAtIndex(result, 0));
                Assert.AreEqual(chunks[1], StringAtIndex(result, 1));
                Assert.AreEqual(0, result.ElementAt(2).Count);
            }

            [Test]
            public void TwoChunksSplitLineBreaks()
            {
                var chunks = new string[] { "adsfasdf\r\n", "\r\n", "asdf" };
                var mockSocket = Mocks.MockSocket(chunks);

                var result = mockSocket.Object.BufferHeaders().Result;

                Assert.AreEqual(3, result.Count);
                Assert.AreEqual(chunks[0], StringAtIndex(result, 0));
                Assert.AreEqual(chunks[1], StringAtIndex(result, 1));
                Assert.AreEqual(0, result.ElementAt(2).Count);
            }

            [Test]
            public void TwoChunksSplitLineBreaks2()
            {
                var chunks = new string[] { "adsfasdf\r\n\r", "\n", "asdf" };
                var mockSocket = Mocks.MockSocket(chunks);

                var result = mockSocket.Object.BufferHeaders().Result;

                Assert.AreEqual(3, result.Count);
                Assert.AreEqual(chunks[0], StringAtIndex(result, 0));
                Assert.AreEqual(chunks[1], StringAtIndex(result, 1));
                Assert.AreEqual(0, result.ElementAt(2).Count);
            }

            [Test]
            public void TwoChunksSplitLineBreaks3()
            {
                var chunks = new string[] { "adsfasdf", "\r\n\r\n", "asdf" };
                var mockSocket = Mocks.MockSocket(chunks);

                var result = mockSocket.Object.BufferHeaders().Result;

                Assert.AreEqual(3, result.Count);
                Assert.AreEqual(chunks[0], StringAtIndex(result, 0));
                Assert.AreEqual(chunks[1], StringAtIndex(result, 1));
                Assert.AreEqual(0, result.ElementAt(2).Count);
            }

            [Test]
            public void SeparatesBodyData()
            {
                var chunks = new string[] { "adsfasdf\r\n\r\nasdf", "asdf" };
                var mockSocket = Mocks.MockSocket(chunks);

                var result = mockSocket.Object.BufferHeaders().Result;

                Assert.AreEqual(2, result.Count);
                Assert.AreEqual("adsfasdf\r\n\r\n", StringAtIndex(result, 0));
                Assert.AreEqual("asdf", StringAtIndex(result, 1));
            }

            [Test]
            public void SeparatesBodyDataTwoChunks()
            {
                var chunks = new string[] { "adsf", "asdf\r\n\r\nasdf", "asdf" };
                var mockSocket = Mocks.MockSocket(chunks);

                var result = mockSocket.Object.BufferHeaders().Result;

                Assert.AreEqual(3, result.Count);
                Assert.AreEqual(chunks[0], StringAtIndex(result, 0));
                Assert.AreEqual("asdf\r\n\r\n", StringAtIndex(result, 1));
                Assert.AreEqual("asdf", StringAtIndex(result, 2));
            }

            [Test]
            public void SeparatesBodyDataTwoChunksSplitLineBreaks()
            {
                var chunks = new string[] { "adsfasdf\r", "\n\r\nasdf", "asdf" };
                var mockSocket = Mocks.MockSocket(chunks);

                var result = mockSocket.Object.BufferHeaders().Result;

                Assert.AreEqual(3, result.Count);
                Assert.AreEqual(chunks[0], StringAtIndex(result, 0));
                Assert.AreEqual("\n\r\n", StringAtIndex(result, 1));
                Assert.AreEqual("asdf", StringAtIndex(result, 2));
            }

            [Test]
            public void SeparatesBodyDataTwoChunksSplitLineBreaks2()
            {
                var chunks = new string[] { "adsfasdf\r\n", "\r\nasddddf", "asdf" };
                var mockSocket = Mocks.MockSocket(chunks);

                var result = mockSocket.Object.BufferHeaders().Result;

                Assert.AreEqual(3, result.Count);
                Assert.AreEqual(chunks[0], StringAtIndex(result, 0));
                Assert.AreEqual("\r\n", StringAtIndex(result, 1));
                Assert.AreEqual("asddddf", StringAtIndex(result, 2));
            }
        }
    }
}
