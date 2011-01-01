using System;
using System.Collections.Generic;
using System.Linq;
using Kayak;
using Moq;
using System.Threading.Tasks;
using System.Text;

namespace KayakTests
{
    class Mocks
    {
        public static Mock<ISocket> MockSocket(params string[] chunks)
        {
            return MockSocket(chunks.Select(s => new ArraySegment<byte>(Encoding.UTF8.GetBytes(s))));
        }

        public static Mock<ISocket> MockSocket(IEnumerable<ArraySegment<byte>> chunks)
        {
            var mockSocket = new Mock<ISocket>();

            IEnumerator<ArraySegment<byte>> cs = null;

            mockSocket
                .Setup(s => s.Read(It.IsAny<byte[]>(), It.IsAny<int>(), It.IsAny<int>()))
                .Returns<byte[], int, int>((byte[] b, int o, int c) =>
                {
                    if (cs == null)
                        cs = chunks.GetEnumerator();

                    if (!cs.MoveNext())
                        return (r, e) => r(0);

                    var chunk = cs.Current;
                    Buffer.BlockCopy(chunk.Array, chunk.Offset, b, o, chunk.Count);
                    return (r, e) => r(chunk.Count);
                });

            return mockSocket;
        }
    }
}
