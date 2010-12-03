using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Owin;

namespace Kayak
{
    public static partial class Extensions
    {
        public static IObservable<IEnumerable<ArraySegment<byte>>> BufferRequestBody(this IRequest request)
        {
            return BufferRequestBodyInternal(request).AsCoroutine<IEnumerable<ArraySegment<byte>>>();
        }

        // wish i had an evented JSON parser.
        static IEnumerable<object> BufferRequestBodyInternal(this IRequest request)
        {
            var result = new LinkedList<ArraySegment<byte>>();
            var buffer = new byte[2048];
            var totalBytesRead = 0;
            var bytesRead = 0;

            var body = ((KayakRequest)request).GetBody();

            foreach (var getChunk in body)
            {
                yield return getChunk(new ArraySegment<byte>(buffer, 0, buffer.Length)).Do(n => bytesRead = n);
                result.AddLast(new ArraySegment<byte>(buffer, 0, bytesRead));
                totalBytesRead += bytesRead;
            }

            yield return result;
        }

        public static string GetString(this IEnumerable<ArraySegment<byte>> buffers)
        {
            return buffers.GetString(Encoding.UTF8);
        }

        public static string GetString(this IEnumerable<ArraySegment<byte>> buffers, Encoding encoding)
        {
            var sb = new StringBuilder(buffers.Sum(s => s.Count));

            foreach (var b in buffers)
                sb.Append(encoding.GetString(b.Array, b.Offset, b.Count));

            return sb.ToString();
        }

        public static string GetString(this ArraySegment<byte> b)
        {
            return b.GetString(Encoding.UTF8);
        }

        public static string GetString(this ArraySegment<byte> b, Encoding encoding)
        {
            return encoding.GetString(b.Array, b.Offset, b.Count);
        }

        internal static IEnumerable<byte> GetBytes(this IEnumerable<ArraySegment<byte>> buffers)
        {
            foreach (var seg in buffers)
                for (int i = seg.Offset; i < seg.Offset + seg.Count; i++)
                    yield return seg.Array[i];
        }
    }
}
