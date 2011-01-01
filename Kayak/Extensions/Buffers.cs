using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Owin;
using Coroutine;

namespace Kayak
{
    public static partial class Extensions
    {
        static int MaxHeaderLength = 1024 * 10;
        static int BufferSize = 1024 * 2;

        /// <summary>
        /// Buffers HTTP headers from a socket. The last ArraySegment in the list is any
        /// data beyond headers which was read, which may be of zero length.
        /// </summary>
        static internal Action<Action<LinkedList<ArraySegment<byte>>>, Action<Exception>> BufferHeaders(this ISocket socket)
        {
            return BufferHeadersInternal(socket).AsContinuation<LinkedList<ArraySegment<byte>>>();
        }

        static IEnumerable<object> BufferHeadersInternal(ISocket socket)
        {
            LinkedList<ArraySegment<byte>> result = new LinkedList<ArraySegment<byte>>();

            int bufferPosition = 0, totalBytesRead = 0;
            byte[] buffer = null;

            while (true)
            {
                if (buffer == null || bufferPosition > BufferSize / 2)
                {
                    buffer = new byte[BufferSize];
                    bufferPosition = 0;
                }

                int bytesRead = 0;

                //Trace.Write("About to read header chunk.");
                var read = new ContinuationState<int>(socket.Read(buffer, bufferPosition, buffer.Length - bufferPosition));
                yield return read;

                bytesRead = read.Result;

                //Trace.Write("Read {0} bytes.", bytesRead);

                result.AddLast(new ArraySegment<byte>(buffer, bufferPosition, bytesRead));

                if (bytesRead == 0)
                    break;

                bufferPosition += bytesRead;
                totalBytesRead += bytesRead;

                // TODO: would be nice to have a state machine and only parse once. for now
                // let's just hope to catch it on the first go-round.
                var bodyDataPosition = IndexOfAfterCRLFCRLF(result);

                if (bodyDataPosition != -1)
                {
                    var last = result.Last.Value;
                    result.RemoveLast();
                    var overlapLength = totalBytesRead - bodyDataPosition;
                    result.AddLast(new ArraySegment<byte>(last.Array, last.Offset, last.Count - overlapLength));
                    result.AddLast(new ArraySegment<byte>(last.Array, last.Offset + last.Count - overlapLength, overlapLength));
                    break;
                }

                // TODO test this
                if (totalBytesRead > MaxHeaderLength)
                    throw new Exception("Request headers data exceeds max header length.");
            }

            yield return result;
        }

        static internal int IndexOfAfterCRLFCRLF(IEnumerable<ArraySegment<byte>> buffers)
        {
            Queue<byte> lastFour = new Queue<byte>(4);

            int i = 0;
            foreach (var b in buffers.GetBytes())
            {
                if (lastFour.Count == 4)
                    lastFour.Dequeue();

                lastFour.Enqueue(b);

                if (lastFour.ElementAt(0) == 0x0d && lastFour.ElementAt(1) == 0x0a &&
                    lastFour.ElementAt(2) == 0x0d && lastFour.ElementAt(3) == 0x0a)
                    return i + 1;

                i++;
            }

            return -1;
        }

        internal static IEnumerable<byte> GetBytes(this IEnumerable<ArraySegment<byte>> buffers)
        {
            foreach (var seg in buffers)
                for (int i = seg.Offset; i < seg.Offset + seg.Count; i++)
                    yield return seg.Array[i];
        }

        internal static string GetString(this IEnumerable<ArraySegment<byte>> buffers)
        {
            return buffers.GetString(Encoding.UTF8);
        }

        internal static string GetString(this IEnumerable<ArraySegment<byte>> buffers, Encoding encoding)
        { 
            var sb = new StringBuilder();

            foreach (var b in buffers)
                sb.Append(encoding.GetString(b.Array, b.Offset, b.Count));

            return sb.ToString();
        }

        internal static string GetString(this ArraySegment<byte> b)
        {
            return b.GetString(Encoding.UTF8);
        }

        internal static string GetString(this ArraySegment<byte> b, Encoding encoding)
        {
            return encoding.GetString(b.Array, b.Offset, b.Count);
        }
    }
}
