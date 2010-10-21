using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Kayak.Core;

namespace Kayak
{
    public static partial class Extensions
    {
        public static IObservable<Unit> WriteAll(this ISocket socket, ArraySegment<byte> chunk)
        {
            return socket.WriteAllInternal(chunk).AsCoroutine<Unit>();
        }

        static IEnumerable<object> WriteAllInternal(this ISocket socket, ArraySegment<byte> chunk)
        {
            int bytesWritten = 0;

            while (bytesWritten < chunk.Count)
            {
                yield return socket.Write(chunk.Array, chunk.Offset + bytesWritten, chunk.Count - bytesWritten)
                    .Do(n => bytesWritten += n);
            }
        }
    }
}
