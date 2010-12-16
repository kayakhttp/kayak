using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Owin;
using System.Threading.Tasks;

namespace Kayak
{
    public static partial class Extensions
    {
        public static Task<IEnumerable<ArraySegment<byte>>> BufferRequestBody(this IRequest request)
        {
            return BufferRequestBodyInternal(request).AsCoroutine<IEnumerable<ArraySegment<byte>>>();
        }

        static IEnumerable<object> BufferRequestBodyInternal(this IRequest request)
        {
            var result = new LinkedList<ArraySegment<byte>>();
            var buffer = new byte[2048];
            var totalBytesRead = 0;
            var bytesRead = 0;

            do
            {
                var read = request.ReadBodyAsync(buffer, 0, buffer.Length);
                yield return read;

                if (read.Exception != null)
                    throw read.Exception;

                bytesRead = read.Result;

                result.AddLast(new ArraySegment<byte>(buffer, 0, bytesRead));
                totalBytesRead += bytesRead;
            }
            while (bytesRead != 0);

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
