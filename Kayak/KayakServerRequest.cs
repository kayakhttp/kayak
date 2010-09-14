using System;
using System.Collections.Generic;
using System.Linq; 

namespace Kayak
{
    /// <summary>
    /// A simple implementation of `IKayakServerRequest`.
    /// </summary>
    public class KayakServerRequest : IKayakServerRequest
    {
        ISocket socket;
        ArraySegment<byte> bodyDataReadWithHeaders;

        HttpRequestLine requestLine;

        string path;
        IDictionary<string, string> queryString;

        public string Verb { get { return requestLine.Verb; } }
        public string RequestUri { get { return requestLine.RequestUri; } }
        public string HttpVersion { get { return requestLine.HttpVersion; } }
        public IDictionary<string, string> Headers { get; private set; }

        #region Derived properties

        public string Path
        {
            get { return path ?? (path = this.GetPath()); }
        }

        public IDictionary<string, string> QueryString
        {
            get { return queryString ?? (queryString = this.GetQueryString()); }
        }

        #endregion

        /// <summary>
        /// Constructs a new `KayakServerRequest` with the given `HttpRequestLine`, headers dictionary,
        /// and `RequestStream`.
        /// </summary>
        public KayakServerRequest(ISocket socket)
        {
            this.socket = socket;
        }

        public IObservable<Unit> Begin()
        {
            return BeginInternal().AsCoroutine<Unit>();
        }

        IEnumerable<object> BeginInternal()
        {
            // would be nice some day to parse the request with a fancy state machine
            // for lower memory usage.

            LinkedList<ArraySegment<byte>> headerBuffers = null;
            yield return socket.ReadHeaders().Do(h => headerBuffers = h);
            bodyDataReadWithHeaders = headerBuffers.Last.Value;
            headerBuffers.RemoveLast();

            HttpRequestLine requestLine;
            IDictionary<string, string> headers;

            headerBuffers.ParseHttpHeaders(out requestLine, out headers);

            this.requestLine = requestLine;
            Headers = headers;
        }

        public IObservable<ArraySegment<byte>> Read()
        {
            return ReadInternal().AsCoroutine<ArraySegment<byte>>();
        }

        IEnumerable<object> ReadInternal()
        {
            if (bodyDataReadWithHeaders.Count > 0)
            {
                var result = bodyDataReadWithHeaders;
                bodyDataReadWithHeaders = default(ArraySegment<byte>);

                yield return result;
                yield break;
            }

            // crazy allocation scheme, anyone?
            byte[] buffer = new byte[1024];

            int bytesRead = 0;

            yield return socket.Read(buffer, 0, buffer.Length).Do(n => bytesRead = n);
            yield return new ArraySegment<byte>(buffer, 0, bytesRead);
        }
    }
}
