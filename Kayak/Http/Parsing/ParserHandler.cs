using System;
using System.Collections.Generic;
using HttpMachine;
using System.Diagnostics;

// XXX all of this should live in HTTP Machine
namespace Kayak.Http
{
    struct HttpRequestHeaders
    {
        public string Method;
        public string Uri;
        public Version Version;
        public IDictionary<string, string> Headers;
    }

    interface IHighLevelParserDelegate
    {
        void OnRequestBegan(HttpRequestHeaders request, bool shouldKeepAlive);
        void OnRequestBody(ArraySegment<byte> data);
        void OnRequestEnded();
    }

    class ParserDelegate : IHttpParserHandler
    {
        string method, requestUri, fragment, queryString, headerName, headerValue;
        IDictionary<string, string> headers;
        IHighLevelParserDelegate del;

        public ParserDelegate(IHighLevelParserDelegate del)
        {
            this.del = del;
        }

        public void OnMessageBegin(HttpParser parser)
        {
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

            var request = new HttpRequestHeaders()
                {
                    // TODO path, query, fragment?
                    Method = method,
                    Uri = requestUri,
                    Headers = headers,
                    Version = new Version(parser.MajorVersion, parser.MinorVersion)
                };

            del.OnRequestBegan(request, parser.ShouldKeepAlive);
        }

        void CommitHeader()
        {
            headers[headerName] = headerValue;
            headerName = headerValue = null;
        }

        public void OnBody(HttpParser parser, ArraySegment<byte> data)
        {
            Debug.WriteLine("OnBody");
            // XXX can we defer this check to the parser?
            if (data.Count > 0)
            {
                del.OnRequestBody(data);
            }
        }

        public void OnMessageEnd(HttpParser parser)
        {
            Debug.WriteLine("OnMessageEnd");
            del.OnRequestEnded();
        }
    }
}
