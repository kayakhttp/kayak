using System.IO;
using System.Web;
using System.Collections.Generic;

namespace Kayak
{
    /// <summary>
    /// A simple implementation of IKayakServerRequest. An `HttpRequestLine`, the headers dictionary, and a `RequestStream`
    /// are provided to the constructor.
    /// </summary>
    public class KayakServerRequest : IKayakServerRequest
    {
        HttpRequestLine requestLine;
        string path;
        IDictionary<string, string> queryString;

        public string Verb { get { return requestLine.Verb; } }
        public string RequestUri { get { return requestLine.RequestUri; } }
        public string HttpVersion { get { return requestLine.HttpVersion; } }
        public IDictionary<string, string> Headers { get; private set; }
        public RequestStream Body { get; private set; }

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
        public KayakServerRequest(HttpRequestLine requestLine, IDictionary<string, string> headers, RequestStream body)
        {
            this.requestLine = requestLine;
            Headers = headers;
            Body = body;
        }
    }
}
