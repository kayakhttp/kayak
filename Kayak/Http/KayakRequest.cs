using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Owin;
using System.IO;

namespace Kayak
{
    public class KayakRequest : IRequest
    {
        public string Method { get; set; }
        public string Uri { get; set; }
        public IDictionary<string, IEnumerable<string>> Headers { get; private set; }
        public IDictionary<string, object> Items { get; private set; }

        ISocket socket;
        ArraySegment<byte> bodyDataReadWithHeaders;

        public static IRequest CreateRequest(ISocket socket, LinkedList<ArraySegment<byte>> headerBuffers)
        {
            var bodyDataReadWithHeaders = headerBuffers.Last.Value;
            headerBuffers.RemoveLast();
            
            var headersString = headerBuffers.GetString();
            var reader = new StringReader(headersString);
            var requestLine = reader.ReadRequestLine();
            var headers = reader.ReadHeaders();
            var context = new Dictionary<string, object>();

            return new KayakRequest(socket, requestLine, headers, context, bodyDataReadWithHeaders);
        }

        public KayakRequest(ISocket socket, HttpRequestLine requestLine, IDictionary<string, IEnumerable<string>> headers, IDictionary<string, object> context, ArraySegment<byte> bodyDataReadWithHeaders)
        {
            this.socket = socket;
            Method = requestLine.Verb;
            Uri = requestLine.RequestUri;
            Headers = headers;
            Items = context;
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

        #region IHttpServerRequest Members

        IEnumerator<Func<ArraySegment<byte>, IObservable<int>>> bodyEnumerator;

        public IAsyncResult BeginReadBody(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
        {
            if (bodyEnumerator == null)
                bodyEnumerator = GetBody().GetEnumerator();

            if (bodyEnumerator.MoveNext())
                return new ObservableAsyncResult<int>(bodyEnumerator.Current(new ArraySegment<byte>(buffer, offset, count)), callback);
            else
            {
                var result = new AsyncResult<int>(0);
                callback(result);
                return result;
            }
        }

        public int EndReadBody(IAsyncResult result)
        {
            return ((AsyncResult<int>)result).GetResult();
        }

        #endregion
    }
}
