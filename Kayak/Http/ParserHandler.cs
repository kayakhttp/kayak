using System;
using System.Collections.Generic;
using HttpMachine;
using System.Diagnostics;

namespace Kayak.Http
{
    struct HttpRequestEvent
    {
        public HttpRequestEventType Type;
        public IRequest Request;
        public bool KeepAlive;
        public ArraySegment<byte> Data;
    }

    enum HttpRequestEventType
    {
        RequestHeaders,
        RequestBody,
        RequestEnded
    }

    class ParserHandler : IHttpParserHandler
    {
        string method, requestUri, fragment, queryString, headerName, headerValue;
        IDictionary<string, string> headers;
        Queue<HttpRequestEvent> events;

        public ParserHandler()
        {
            events = new Queue<HttpRequestEvent>();
        }

        public bool HasEvents { get { return events.Count > 0; } }

        public HttpRequestEvent GetNextEvent()
        {
            return events.Dequeue();
        }

        public void OnMessageBegin(HttpParser parser)
        {
            //states.Enqueue(new HttpState() { Kind = HttpStateKind.RequestBegan });
            method = requestUri = fragment = queryString = headerName = headerValue = null;
            headers = null;
        }

        public void OnMethod(HttpParser parser, string method)
        {
            this.method = method;
        }

        public void OnRequestUri(HttpParser parser, string requestUri)
        {
            this.requestUri = requestUri;
        }

        public void OnFragment(HttpParser parser, string fragment)
        {
            this.fragment = fragment;
        }

        public void OnQueryString(HttpParser parser, string queryString)
        {
            this.queryString = queryString;
        }

        public void OnHeaderName(HttpParser parser, string name)
        {
            if (headers == null)
                headers = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);

            if (!string.IsNullOrEmpty(headerValue))
                CommitHeader();

            headerName = name;
        }

        public void OnHeaderValue(HttpParser parser, string value)
        {
            if (string.IsNullOrEmpty(headerName))
                throw new Exception("Got header value without name.");

            headerValue = value;
        }

        public void OnHeadersEnd(HttpParser parser)
        {
            Debug.WriteLine("OnHeadersEnd");

            if (!string.IsNullOrEmpty(headerValue))
                CommitHeader();

            events.Enqueue(new HttpRequestEvent()
            {
                Type = HttpRequestEventType.RequestHeaders,
                KeepAlive = parser.ShouldKeepAlive,
                Request = new Request()
                {
                    // TODO path, query, fragment?
                    Method = method,
                    Uri = requestUri,
                    Headers = headers,
                    Version = new Version(parser.MajorVersion, parser.MinorVersion)
                },
            });
        }

        void CommitHeader()
        {
            headers[headerName] = headerValue;
            headerName = headerValue = null;
        }

        public void OnBody(HttpParser parser, ArraySegment<byte> data)
        {
            // XXX can we defer this check to the parser?
            if (data.Count > 0)
            {
                events.Enqueue(new HttpRequestEvent()
                {
                    Type = HttpRequestEventType.RequestBody,
                    Data = data
                });
            }
        }

        public void OnMessageEnd(HttpParser parser)
        {
            events.Enqueue(new HttpRequestEvent()
            {
                Type = HttpRequestEventType.RequestEnded,
                KeepAlive = parser.ShouldKeepAlive
            });
        }
    }
}
