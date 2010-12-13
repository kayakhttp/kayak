using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Moq;
using Kayak;
using Owin;

namespace KayakTests.Extensions
{
    [TestFixture]
    public class BufferTests
    {
        byte[] body = Encoding.UTF8.GetBytes("This is the body!!!");

        public IEnumerable<Func<ArraySegment<byte>, IObservable<int>>> RequestBody()
        {
            int position = 0;

            while (position < body.Length)
                yield return (seg) =>
                    {
                        var count = Math.Min(position - body.Length, seg.Count);
                        Buffer.BlockCopy(body, position, seg.Array, seg.Offset, count);
                        position += count;
                        return new int[] { count }.ToObservable();
                    };
            
        }

        public bool AreEqual(IEnumerable<byte> a, IEnumerable<byte> b)
        {
            return false;
        }

        [Test]
        public void TestBufferRequestBody()
        {
            var mockRequest = new Mock<IRequest>();
            var mockObserver = new Mock<IObserver<IEnumerable<ArraySegment<byte>>>>();

            Assert.Fail("Borken.");
            //mockRequest.Setup(r => r.GetBody()).Returns(() => RequestBody());

            mockObserver.Setup(r => r.OnNext(It.Is<IEnumerable<ArraySegment<byte>>>(b => AreEqual(b.GetBytes(), body))));

            var bufferObservable = mockRequest.Object.BufferRequestBody();

            bufferObservable.Subscribe(mockObserver.Object);

            //mockRequest.Verify(r => r.GetBody());
        }
    }
}
