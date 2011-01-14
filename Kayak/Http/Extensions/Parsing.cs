using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Coroutine;

namespace Kayak
{
    /// <summary>
    /// Represents the first line of an HTTP request. Used when constructing a `KayakRequest`.
    /// </summary>
    public struct HttpRequestLine
    {
        /// <summary>
        /// The verb component of the request line (e.g., GET, POST, etc).
        /// </summary>
        public string Verb;
        /// <summary>
        /// The request URI component of the request line (e.g., /path/and?query=string).
        /// </summary>
        public string RequestUri;

        /// <summary>
        /// The HTTP version component of the request line (e.g., HTTP/1.0).
        /// </summary>
        public string HttpVersion;
    }

    public static partial class Extensions
    {
        /// <summary>
        /// Buffers HTTP headers from a socket. The last ArraySegment in the list is any
        /// data beyond headers which was read, which may be of zero length.
        /// </summary>
        static internal Action<Action<LinkedList<ArraySegment<byte>>>, Action<Exception>> BufferHeaders(this ISocket socket)
        {
            return BufferHeadersInternal(socket).AsContinuation<LinkedList<ArraySegment<byte>>>();
        }

        // this is horribly inefficient right now.
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

        internal static HttpRequestLine ReadRequestLine(this TextReader reader)
        {
            string statusLine = reader.ReadLine();

            if (string.IsNullOrEmpty(statusLine))
                throw new Exception("Could not parse request status.");

            var tokens = statusLine.Split(' ').Where(s => !string.IsNullOrEmpty(s)).ToArray();

            if (tokens.Length != 3 && tokens.Length != 2)
                throw new Exception("Expected 2 or 3 tokens in request line.");

            return new HttpRequestLine()
                {
                    Verb = tokens[0],
                    RequestUri = tokens[1],
                    HttpVersion = tokens.Length == 3 ? tokens[2] : "HTTP/1.0"
                };
        }

        public static IDictionary<string, IList<string>> ReadHeaders(this TextReader reader)
        {
            var headers = new Dictionary<string, IList<string>>();
            string line = null;

            while (!string.IsNullOrEmpty(line = reader.ReadLine()))
            {
                int colon = line.IndexOf(':');
                headers.Add(line.Substring(0, colon), new string[] { line.Substring(colon + 1).Trim() });
            }

            return headers;
        }

        public static byte[] WriteStatusLineAndHeaders(string status, IDictionary<string, IList<string>> headers)
        {
            var sb = new StringBuilder();

            sb.AppendFormat("HTTP/1.0 {0}\r\n", status);

            if (!headers.ContainsKey("Server"))
                headers["Server"] = new string[] { "Kayak" };

            if (!headers.ContainsKey("Date"))
                headers["Date"] = new string[] { DateTime.UtcNow.ToString() };

            foreach (var pair in headers)
                foreach (var line in pair.Value)
                sb.AppendFormat("{0}: {1}\r\n", pair.Key, line);

            sb.Append("\r\n");

            return Encoding.UTF8.GetBytes(sb.ToString());
        }
    }
}
