using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Owin;

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

        IEnumerator<Func<ArraySegment<byte>, IObservable<int>>> bodyEnumerator;

        public IAsyncResult BeginReadBody(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
        {
            int bytesRead;
            if (bodyDataReadWithHeaders.Count > 0)
            {
                //Buffer.BlockCopy(bodyDataReadWithHeaders,
            }

            return null;
        }

        public int EndReadBody(IAsyncResult result)
        {
            return 0;
        }
    }
}
