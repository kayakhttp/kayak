using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Web;

namespace Kayak
{
    public static partial class Extensions
    {
        static int MaxHeaderLength = 1024 * 10;
        static int BufferSize = 1024 * 2;

        // last ArraySegment is any data beyond headers which was read, which may be of zero length.
        public static IObservable<List<ArraySegment<byte>>> ReadHeaders(this ISocket socket)
        {
            return socket.ReadHeadersInternal().AsCoroutine<List<ArraySegment<byte>>>();
        }

        static IEnumerable<object> ReadHeadersInternal(this ISocket socket)
        {
            List<ArraySegment<byte>> result = new List<ArraySegment<byte>>();

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

                if (bytesRead == 0)
                    break;

                result.Add(new ArraySegment<byte>(buffer, bufferPosition, bytesRead));

                bufferPosition += bytesRead;
                totalBytesRead += bytesRead;

                var bodyDataPosition = result.IndexOfAfterCRLFCRLF();

                if (bodyDataPosition != -1)
                {
                    //Console.WriteLine("Read " + totalBytesRead + ", body starts at " + bodyDataPosition);
                    var last = result[result.Count - 1];
                    var overlapLength = totalBytesRead - bodyDataPosition;
                    result[result.Count - 1] = new ArraySegment<byte>(last.Array, last.Offset, last.Count - overlapLength); 
                    result.Add(new ArraySegment<byte>(last.Array, last.Offset + last.Count - overlapLength, overlapLength)); 
                    break;
                }

                // TODO test this
                if (totalBytesRead > MaxHeaderLength)
                    throw new Exception("Request headers data exceeds max header length.");
            }

            yield return result;
        }

        public static int IndexOfAfterCRLFCRLF(this IEnumerable<ArraySegment<byte>> buffers)
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
            var sb = new StringBuilder(buffers.Sum(s => s.Count));

            foreach (var b in buffers)
                sb.Append(Encoding.ASCII.GetString(b.Array, b.Offset, b.Count));

            return sb.ToString();
        }

        public static IObservable<KayakServerRequest> CreateRequest(this ISocket socket)
        {
            return CreateRequestInternal(socket).AsCoroutine<KayakServerRequest>();
        }

        static IEnumerable<object> CreateRequestInternal(ISocket socket)
        {
            List<ArraySegment<byte>> headerBuffer = null;
            yield return socket.ReadHeaders().Do(h => headerBuffer = h);

            var overlap = headerBuffer.Last();
            headerBuffer.Remove(overlap);
            
            HttpRequestLine requestLine;
            NameValueDictionary headers;
            headerBuffer.GetString().ParseHttpHeaders(out requestLine, out headers);

            RequestStream requestBody = null;
            var contentLength = headers.GetContentLength();
            Trace.Write("Got request with content length " + contentLength);

            // TODO what if content-length is not given (i.e., chunked transfer)
            // prolly something like:
            // if (overlap.Count > 0)
            if (contentLength > 0)
            {
                Trace.Write("Creating body stream with overlap " + overlap.Count + ", contentLength " + contentLength);
                requestBody = new RequestStream(socket, overlap, contentLength);
            }

            yield return new KayakServerRequest(requestLine, headers, requestBody);
        }

        public static void ParseHttpHeaders(this string s, out HttpRequestLine requestLine, out NameValueDictionary headers)
        {
            var reader = new StringReader(s);

            string statusLine = reader.ReadLine();

            if (string.IsNullOrEmpty(statusLine))
                throw new Exception("Could not parse request status.");

            int firstSpace = statusLine.IndexOf(' ');
            int lastSpace = statusLine.LastIndexOf(' ');

            if (firstSpace == -1 || lastSpace == -1)
                throw new Exception("Could not parse request status.");

            requestLine = new HttpRequestLine();
            requestLine.Verb = statusLine.Substring(0, firstSpace);

            bool hasVersion = lastSpace != firstSpace;

            if (hasVersion)
                requestLine.HttpVersion = statusLine.Substring(lastSpace + 1);
            else
                requestLine.HttpVersion = "HTTP/1.0";

            requestLine.RequestUri = hasVersion
                ? statusLine.Substring(firstSpace + 1, lastSpace - firstSpace - 1)
                : statusLine.Substring(firstSpace + 1);

            headers = new NameValueDictionary();
            string line = null;

            while (!string.IsNullOrEmpty(line = reader.ReadLine()))
            {
                int colon = line.IndexOf(':');
                headers.Add(line.Substring(0, colon), line.Substring(colon + 1).Trim());
            }

            headers.BecomeReadOnly();
        }

        public static string GetPath(this IKayakServerRequest request)
        {
            int question = request.RequestUri.IndexOf('?');
            return HttpUtility.UrlDecode(question >= 0 ? request.RequestUri.Substring(0, question) : request.RequestUri);
        }

        public static NameValueDictionary GetQueryString(this IKayakServerRequest request)
        {
            int question = request.RequestUri.IndexOf('?');
            return question >= 0 ?
                request.RequestUri.DecodeQueryString(question + 1, request.RequestUri.Length - question - 1) :
                new NameValueDictionary();
        }
    }
}
