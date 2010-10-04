using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Kayak.Core;

namespace Kayak
{
    public class KayakRequest : IHttpServerRequest
    {
        public string Verb { get; set; }
        public string RequestUri { get; set; }
        public string HttpVersion { get; set; }
        public IDictionary<string, string> Headers { get; private set; }

        ISocket socket;
        ArraySegment<byte> bodyDataReadWithHeaders;

        public KayakRequest(ISocket socket, HttpRequestLine requestLine, IDictionary<string, string> headers, ArraySegment<byte> bodyDataReadWithHeaders)
        {
            this.socket = socket;
            Verb = requestLine.Verb;
            RequestUri = requestLine.RequestUri;
            HttpVersion = requestLine.HttpVersion;
            Headers = headers;
            this.bodyDataReadWithHeaders = bodyDataReadWithHeaders;
        }

        public IEnumerable<Func<byte[], int, int,IObservable<int>>> GetBody()
        {
            var contentLength = Headers.GetContentLength();
            var totalBytesRead = 0;

            if (bodyDataReadWithHeaders.Count > 0)
            {
                yield return (buffer, offset, count) =>
                    {
                        var toRead = (int)Math.Min(count, bodyDataReadWithHeaders.Count);

                        Buffer.BlockCopy(bodyDataReadWithHeaders.Array, bodyDataReadWithHeaders.Offset, buffer, offset, toRead);

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
                    yield return (buffer, offset, count) => socket.Read(buffer, offset, count).Do(n => bytesRead = n);
                    totalBytesRead += bytesRead;
                } while (bytesRead != 0 && (contentLength == -1 || totalBytesRead < contentLength));
            }
        }
    }
}
