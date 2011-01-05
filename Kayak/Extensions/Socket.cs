using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Coroutine;

namespace Kayak
{
    public static partial class Extensions
    {
        internal static ContinuationState<Unit> WriteChunk(this ISocket socket, ArraySegment<byte> chunk)
        {
            return socket.WriteChunkInternal(chunk).AsCoroutine<Unit>();
        }

        static IEnumerable<object> WriteChunkInternal(this ISocket socket, ArraySegment<byte> chunk)
        {
            int bytesWritten = 0;

            while (bytesWritten < chunk.Count)
            {
                var write = new ContinuationState<int>(socket.Write(chunk.Array, chunk.Offset + bytesWritten, chunk.Count - bytesWritten));
                yield return write;

                bytesWritten += write.Result;
            }

            yield return default(Unit);
        }
    }
}
