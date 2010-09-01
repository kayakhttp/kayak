using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Core;
using NUnit.Framework;
using Moq;
using Kayak;

namespace KayakTests.Extensions
{
    [TestFixture]
    public class RequestTests
    {
        [TestFixture]
        public class ReadHeadersTests
        {
            string headers = "1234567890\r\n\r\n";
            string rest = "asdfjkl;";
            byte[] buffer;
            List<ArraySegment<byte>> chunks;
            Mock<ISocket> mockSocket;

            bool gotException, gotCompleted, gotResult;
            Exception exception;
            List<ArraySegment<byte>> result;

            [SetUp]
            public void SetUp()
            {
                buffer = Encoding.ASCII.GetBytes(headers + rest);

                chunks = new List<ArraySegment<byte>>();
                mockSocket = new Mock<ISocket>();

                var readCount = 0;

                mockSocket
                    .Setup(s => s.Read(It.IsAny<byte[]>(), It.IsAny<int>(), It.IsAny<int>()))
                    .Returns<byte[], int, int>((byte[] b, int o, int c) =>
                    {
                        return Observable.Create<int>(ob => {
                            var chunk = chunks[readCount++];
                            Buffer.BlockCopy(chunk.Array, chunk.Offset, b, o, chunk.Count);
                            ob.OnNext(chunk.Count);
                            ob.OnCompleted();
                            return null;
                        });
                    }).Verifiable();

                gotException = gotCompleted = gotResult = false;
                exception = null;
                result = null;
            }

            void DoRead()
            {
                mockSocket.Object.ReadHeaders().Subscribe(
                    b => { gotResult = true; result = b; },
                    e => { gotException = true; exception = e; Console.Out.WriteException(e); },
                    () => { gotCompleted = true; });
            }

            void AssertResult()
            {
                Assert.AreEqual(headers, result.Take(result.Count - 1).GetString(), "Incorrect header result.");
            }

            void AssertRest(int length)
            {
                Assert.AreEqual(rest.Substring(0, length), result.Skip(result.Count - 1).GetString(), "Incorrect rest result.");
            }

            void AssertObservableBehavior()
            {
                Assert.IsFalse(gotException, "Unexpected exception.");
                Assert.IsTrue(gotResult, "Didn't get result.");
                Assert.IsTrue(gotCompleted, "Didn't get completed.");
            }

            [Test]
            public void SingleReadNoOverlap()
            {
                chunks.Add(new ArraySegment<byte>(buffer, 0, 14));

                DoRead();

                AssertObservableBehavior();
                Assert.AreEqual(2, result.Count, "Unexpected result buffer count.");
                AssertResult();
                Assert.AreEqual(14, result[0].Count, "Unexpected header buffer length.");
                AssertRest(0);
            }

            [Test]
            public void SingleReadSomeOverlap()
            {
                chunks.Add(new ArraySegment<byte>(buffer, 0, 16));

                DoRead();

                AssertObservableBehavior();
                Assert.AreEqual(2, result.Count, "Unexpected result buffer count.");
                AssertResult();
                Assert.AreEqual(14, result[0].Count, "Unexpected header buffer length.");
                AssertRest(2);
            }

            [Test]
            public void TwoReadNoOverlap()
            {
                chunks.Add(new ArraySegment<byte>(buffer, 0, 7));
                chunks.Add(new ArraySegment<byte>(buffer, 7, 7));

                DoRead();

                AssertObservableBehavior();
                Assert.AreEqual(3, result.Count, "Unexpected result buffer count.");
                AssertResult();
                Assert.AreEqual(7, result[0].Count, "Unexpected header buffer length.");
                Assert.AreEqual(7, result[1].Count, "Unexpected header buffer length.");
                AssertRest(0);
            }

            [Test]
            public void TwoReadSomeOverlap()
            {
                chunks.Add(new ArraySegment<byte>(buffer, 0, 7));
                chunks.Add(new ArraySegment<byte>(buffer, 7, 9));

                DoRead();

                AssertObservableBehavior();
                Assert.AreEqual(3, result.Count, "Unexpected result buffer count.");
                AssertResult();
                Assert.AreEqual(7, result[0].Count, "Unexpected header buffer length.");
                Assert.AreEqual(7, result[1].Count, "Unexpected header buffer length.");
                AssertRest(2);
            }

            [Test]
            public void TwoReadSplitBreakNoOverlap()
            {
                chunks.Add(new ArraySegment<byte>(buffer, 0, 11));
                chunks.Add(new ArraySegment<byte>(buffer, 11, 3));

                DoRead();

                AssertObservableBehavior();
                Assert.AreEqual(3, result.Count, "Unexpected result buffer count.");
                AssertResult();
                Assert.AreEqual(11, result[0].Count, "Unexpected header buffer length.");
                Assert.AreEqual(3, result[1].Count, "Unexpected header buffer length.");
                AssertRest(0);
            }

            [Test]
            public void TwoReadSplitBreakSomeOverlap()
            {
                chunks.Add(new ArraySegment<byte>(buffer, 0, 11));
                chunks.Add(new ArraySegment<byte>(buffer, 11, 6));

                DoRead();

                AssertObservableBehavior();
                Assert.AreEqual(3, result.Count, "Unexpected result buffer count.");
                AssertResult();
                Assert.AreEqual(11, result[0].Count, "Unexpected header buffer length.");
                Assert.AreEqual(3, result[1].Count, "Unexpected header buffer length.");
                AssertRest(3);
            }
        }

        [TestFixture]
        public class IndexOfAfterCRLFCRLFTests
        {
            byte[] buffer;
            List<ArraySegment<byte>> buffers;

            [SetUp]
            public void SetUp()
            {
                buffer = Encoding.ASCII.GetBytes("1234567890\r\n\r\nasdfjkl;");
                buffers = new List<ArraySegment<byte>>();
            }

            [Test]
            public void ContainsCRLFCRLFPositive1Seg()
            {
                buffers.Add(new ArraySegment<byte>(buffer, 0, 14));

                Assert.AreEqual(14, buffers.IndexOfAfterCRLFCRLF());
            }

            [Test]
            public void ContainsCRLFCRLFPositive2Seg()
            {
                buffers.Add(new ArraySegment<byte>(buffer, 0, 4));
                buffers.Add(new ArraySegment<byte>(buffer, 4, 10));

                Assert.AreEqual(14, buffers.IndexOfAfterCRLFCRLF());
            }

            [Test]
            public void ContainsCRLFCRLFPositive3Seg()
            {
                buffers.Add(new ArraySegment<byte>(buffer, 0, 5));
                buffers.Add(new ArraySegment<byte>(buffer, 5, 5));
                buffers.Add(new ArraySegment<byte>(buffer, 10, 5));

                Assert.AreEqual(14, buffers.IndexOfAfterCRLFCRLF());
            }

            [Test]
            public void ContainsCRLFCRLFPositive2SegSplit()
            {
                buffers.Add(new ArraySegment<byte>(buffer, 0, 11));
                buffers.Add(new ArraySegment<byte>(buffer, 11, 5));

                Assert.AreEqual(14, buffers.IndexOfAfterCRLFCRLF());
            }

            [Test]
            public void ContainsCRLFCRLFPositive3SegSplit()
            {
                buffers.Add(new ArraySegment<byte>(buffer, 0, 11));
                buffers.Add(new ArraySegment<byte>(buffer, 11, 1));
                buffers.Add(new ArraySegment<byte>(buffer, 12, 3));

                Assert.AreEqual(14, buffers.IndexOfAfterCRLFCRLF());
            }

            [Test]
            public void ContainsCRLFCRLFNegative1Seg()
            {
                buffers.Add(new ArraySegment<byte>(buffer, 0, 5));

                Assert.AreEqual(-1, buffers.IndexOfAfterCRLFCRLF());
            }

            [Test]
            public void ContainsCRLFCRLFNegative2Seg()
            {
                buffers.Add(new ArraySegment<byte>(buffer, 0, 5));
                buffers.Add(new ArraySegment<byte>(buffer, 5, 2));

                Assert.AreEqual(-1, buffers.IndexOfAfterCRLFCRLF());
            }

            [Test]
            public void ContainsCRLFCRLFNegative3Seg()
            {
                buffers.Add(new ArraySegment<byte>(buffer, 0, 5));
                buffers.Add(new ArraySegment<byte>(buffer, 5, 2));
                buffers.Add(new ArraySegment<byte>(buffer, 7, 4));

                Assert.AreEqual(-1, buffers.IndexOfAfterCRLFCRLF());
            }
        }
    }
}
