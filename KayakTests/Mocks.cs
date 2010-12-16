//using System;
//using System.Collections.Generic;
//using System.Linq;
//using Kayak;
//using Moq;

//namespace KayakTests
//{
//    class Mocks
//    {
//        public static Mock<ISocket> MockSocketRead(IEnumerable<ArraySegment<byte>> chunks)
//        {
//            IEnumerator<ArraySegment<byte>> cs = null;

//            var mockSocket = new Mock<ISocket>();
//            mockSocket
//                .Setup(s => s.Read(It.IsAny<byte[]>(), It.IsAny<int>(), It.IsAny<int>()))
//                .Returns<byte[], int, int>((byte[] b, int o, int c) =>
//                {
//                    if (cs == null)
//                        cs = chunks.GetEnumerator();

//                    return Observable.Create<int>(ob =>
//                    {
//                        if (!cs.MoveNext())
//                        {
//                            ob.OnNext(0);
//                            ob.OnCompleted();
//                        }
//                        else
//                        {
//                            var chunk = cs.Current;
//                            Buffer.BlockCopy(chunk.Array, chunk.Offset, b, o, chunk.Count);
//                            ob.OnNext(chunk.Count);
//                            ob.OnCompleted();
//                        }

//                        return () => { cs.Dispose(); };
//                    });
//                });

//            return mockSocket;
//        }
//    }
//}
