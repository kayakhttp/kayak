using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Kayak.Core;
using System.IO;

namespace Kayak
{
    public class KayakRequest : IHttpServerRequest
    {
        public string Verb { get; set; }
        public string RequestUri { get; set; }
        public string HttpVersion { get; set; }
        public IDictionary<string, string> Headers { get; private set; }
        public IDictionary<object, object> Context { get; private set; }

        ISocket socket;
        ArraySegment<byte> bodyDataReadWithHeaders;

        public static IHttpServerRequest CreateRequest(ISocket socket, LinkedList<ArraySegment<byte>> headerBuffers)
        {
            var bodyDataReadWithHeaders = headerBuffers.Last.Value;
            headerBuffers.RemoveLast();

            IHttpServerRequest request = null;

            var context = new Dictionary<object, object>();

            var headersString = headerBuffers.GetString();
            using (var reader = new StringReader(headersString))
                request = new KayakRequest(socket, reader.ReadRequestLine(), reader.ReadHeaders(), context, bodyDataReadWithHeaders);

            return request;
        }


        public KayakRequest(ISocket socket, HttpRequestLine requestLine, IDictionary<string, string> headers, IDictionary<object, object> context, ArraySegment<byte> bodyDataReadWithHeaders)
        {
            this.socket = socket;
            Verb = requestLine.Verb;
            RequestUri = requestLine.RequestUri;
            HttpVersion = requestLine.HttpVersion;
            Headers = headers;
            Context = context;
            this.bodyDataReadWithHeaders = bodyDataReadWithHeaders;
        }

        public IEnumerable<Func<ArraySegment<byte>, IObservable<int>>> GetBody()
        {
            var contentLength = Headers.GetContentLength();
            var totalBytesRead = 0;

            if (bodyDataReadWithHeaders.Count > 0)
            {
                yield return seg =>
                    {
                        var toRead = (int)Math.Min(seg.Count, bodyDataReadWithHeaders.Count);

                        Buffer.BlockCopy(bodyDataReadWithHeaders.Array, bodyDataReadWithHeaders.Offset, seg.Array, seg.Offset, toRead);

                        var remaining = bodyDataReadWithHeaders.Count - toRead;

                        if (remaining > 0)
                            bodyDataReadWithHeaders = new ArraySegment<byte>(
                                bodyDataReadWithHeaders.Array,
                                bodyDataReadWithHeaders.Offset + toRead,
                                remaining);
                        else
                            bodyDataReadWithHeaders = default(ArraySegment<byte>);

                        totalBytesRead += toRead;
                        return new int[] { toRead }.ToObservable();
                    };
            }

            if (contentLength == -1 || totalBytesRead < contentLength)
            {
                var bytesRead = 0;
                do
                {
                    yield return (seg) => socket.Read(seg.Array, seg.Offset, seg.Count).Do(n => bytesRead = n);
                    totalBytesRead += bytesRead;
                } while (bytesRead != 0 && (contentLength == -1 || totalBytesRead < contentLength));
            }
        }
    }
}
