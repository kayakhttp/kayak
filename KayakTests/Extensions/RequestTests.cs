using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Core;
using NUnit.Framework;
using Moq;
using Kayak;
using System.Disposables;

namespace KayakTests.Extensions
{
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
            Mock<ISubject<LinkedList<ArraySegment<byte>>>> mockSubject;

            [SetUp]
            public void SetUp()
            {
                buffer = Encoding.ASCII.GetBytes(headers + rest);

                chunks = new List<ArraySegment<byte>>();
                mockSocket = new Mock<ISocket>();
                mockSubject = new Mock<ISubject<LinkedList<ArraySegment<byte>>>>();
                mockSubject.Setup(s => s.OnError(It.IsAny<Exception>())).Callback<Exception>(e => Console.Out.WriteException(e));
                
                mockSocket = Mocks.MockSocketRead(chunks);
            }

            void DoRead()
            {
                mockSocket.Object.ReadHeaders().Subscribe(mockSubject.Object);
                Console.WriteLine("After read headers subscribe.");
            }

            void AssertResult()
            {
                //Assert.AreEqual(headers, result.Take(result.Count - 1).GetString(), "Incorrect header result.");
            }

            void AssertRest(int length)
            {
                //Assert.AreEqual(rest.Substring(0, length), result.Skip(result.Count - 1).GetString(), "Incorrect rest result.");
            }

            void AssertObservableBehavior()
            {
                //Assert.IsFalse(gotException, "Unexpected exception.");
                //Assert.IsTrue(gotResult, "Didn't get result.");
                //Assert.IsTrue(gotCompleted, "Didn't get completed.");
            }

            bool MatchesChunks(LinkedList<ArraySegment<byte>> result, string[] expectedChunks)
            {
                var i = 0;
                foreach (var c in expectedChunks)
                {
                    var seg = result.ElementAt(i++);
                    if (c != Encoding.UTF8.GetString(seg.Array, seg.Offset, seg.Count))
                        return false;
                }
                return true;
            }

            void VerifySubject(string[] expectedChunks)
            {
                mockSubject.Verify(s => s.OnError(It.IsAny<Exception>()), Times.Never(), "Subject got exception.");
                mockSubject.Verify(s => s.OnNext(
                    It.Is<LinkedList<ArraySegment<byte>>>(l => MatchesChunks(l, expectedChunks))), 
                    Times.Once(), 
                    "Didn't get correct result.");

                mockSubject.Verify(s => s.OnCompleted());
            }

            [Test]
            public void SingleReadNoOverlap()
            {
                chunks.Add(new ArraySegment<byte>(buffer, 0, 14));

                DoRead();

                VerifySubject(new string[] { headers });
            }

            [Test]
            public void SingleReadSomeOverlap()
            {
                chunks.Add(new ArraySegment<byte>(buffer, 0, 16));

                DoRead();

                VerifySubject(new string[] { headers, rest.Substring(0, 2) });

                //AssertObservableBehavior();
                //Assert.AreEqual(2, result.Count, "Unexpected result buffer count.");
                //AssertResult();
                //Assert.AreEqual(14, result[0].Count, "Unexpected header buffer length.");
                //AssertRest(2);
            }

            [Test]
            public void TwoReadNoOverlap()
            {
                chunks.Add(new ArraySegment<byte>(buffer, 0, 7));
                chunks.Add(new ArraySegment<byte>(buffer, 7, 7));

                DoRead();

                VerifySubject(new string[] { headers.Substring(0, 7), headers.Substring(7, 7) });

                //AssertObservableBehavior();
                //Assert.AreEqual(3, result.Count, "Unexpected result buffer count.");
                //AssertResult();
                //Assert.AreEqual(7, result[0].Count, "Unexpected header buffer length.");
                //Assert.AreEqual(7, result[1].Count, "Unexpected header buffer length.");
                //AssertRest(0);
            }

            [Test]
            public void TwoReadSomeOverlap()
            {
                chunks.Add(new ArraySegment<byte>(buffer, 0, 7));
                chunks.Add(new ArraySegment<byte>(buffer, 7, 9));

                DoRead();

                VerifySubject(new string[] { headers.Substring(0, 7), headers.Substring(7, 7), rest.Substring(0, 2) });

                //AssertObservableBehavior();
                //Assert.AreEqual(3, result.Count, "Unexpected result buffer count.");
                //AssertResult();
                //Assert.AreEqual(7, result[0].Count, "Unexpected header buffer length.");
                //Assert.AreEqual(7, result[1].Count, "Unexpected header buffer length.");
                //AssertRest(2);
            }

            [Test]
            public void TwoReadSplitBreakNoOverlap()
            {
                chunks.Add(new ArraySegment<byte>(buffer, 0, 11));
                chunks.Add(new ArraySegment<byte>(buffer, 11, 3));

                DoRead();

                VerifySubject(new string[] { headers.Substring(0, 11), headers.Substring(11, 3) });

                //AssertObservableBehavior();
                //Assert.AreEqual(3, result.Count, "Unexpected result buffer count.");
                //AssertResult();
                //Assert.AreEqual(11, result[0].Count, "Unexpected header buffer length.");
                //Assert.AreEqual(3, result[1].Count, "Unexpected header buffer length.");
                //AssertRest(0);
            }

            [Test]
            public void TwoReadSplitBreakSomeOverlap()
            {
                chunks.Add(new ArraySegment<byte>(buffer, 0, 11));
                chunks.Add(new ArraySegment<byte>(buffer, 11, 6));

                DoRead();

                VerifySubject(new string[] { headers.Substring(0, 11), headers.Substring(11, 3), rest.Substring(0, 3) });

                //AssertObservableBehavior();
                //Assert.AreEqual(3, result.Count, "Unexpected result buffer count.");
                //AssertResult();
                //Assert.AreEqual(11, result[0].Count, "Unexpected header buffer length.");
                //Assert.AreEqual(3, result[1].Count, "Unexpected header buffer length.");
                //AssertRest(3);
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
