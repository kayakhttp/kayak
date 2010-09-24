using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Web;

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
        internal static IObservable<LinkedList<ArraySegment<byte>>> ReadHeaders(this ISocket socket)
        {
            return socket.ReadHeadersInternal().AsCoroutine<LinkedList<ArraySegment<byte>>>();
        }

        static IEnumerable<object> ReadHeadersInternal(this ISocket socket)
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

                Trace.Write("About to read header chunk.");
                yield return socket.Read(buffer, bufferPosition, buffer.Length - bufferPosition).Do(n => bytesRead = n);
                Trace.Write("Read {0} bytes.", bytesRead);

                result.AddLast(new ArraySegment<byte>(buffer, bufferPosition, bytesRead));

                if (bytesRead == 0)
                    break;

                bufferPosition += bytesRead;
                totalBytesRead += bytesRead;

                var bodyDataPosition = result.IndexOfAfterCRLFCRLF();

                if (bodyDataPosition != -1)
                {
                    //Console.WriteLine("Read " + totalBytesRead + ", body starts at " + bodyDataPosition);
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

            Trace.Write("Yielding result.");
            yield return result;
            Trace.Write("Yielded result.");
        }

        internal static int IndexOfAfterCRLFCRLF(this IEnumerable<ArraySegment<byte>> buffers)
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

        static IEnumerable<byte> GetBytes(this IEnumerable<ArraySegment<byte>> buffers)
        {
            foreach (var seg in buffers)
                for (int i = seg.Offset; i < seg.Offset + seg.Count; i++)
                    yield return seg.Array[i];
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

        /// <summary>
        /// Returns the path component from the `RequestUri` property of the `IKayakServerRequest` (e.g., /some/path).
        /// </summary>
        public static string GetPath(this IKayakServerRequest request)
        {
            int question = request.RequestUri.IndexOf('?');
            return HttpUtility.UrlDecode(question >= 0 ? request.RequestUri.Substring(0, question) : request.RequestUri);
        }

        /// <summary>
        /// Returns a dictionary representation of the query string component from the `RequestUri`
        /// property of the `IKayakServerRequest`.
        /// </summary>
        public static IDictionary<string, string> GetQueryString(this IKayakServerRequest request)
        {
            int question = request.RequestUri.IndexOf('?');
            return question >= 0 ?
                request.RequestUri.ParseQueryString(question + 1, request.RequestUri.Length - question - 1) :
                new Dictionary<string, string>();
        }
    }
}
