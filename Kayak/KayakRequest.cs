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

        public IObservable<int> GetBodyChunk(byte[] buffer, int offset, int count)
        {
            if (bodyDataReadWithHeaders.Count > 0)
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

                return new int[] { toRead }.ToObservable();
            }

            return socket.Read(buffer, offset, count);
        }
    }
}
