using System.IO;
using System.Web;
using System.Collections.Generic;

namespace Kayak
{
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

        /// <summary>
        /// Get the Uri Path for this request, i.e. "/some/path" without querystring or "http://".
        /// </summary>
        public string Path
        {
            get { return path ?? (path = this.GetPath()); }
        }

        /// <summary>
        /// Gets the collection of parameters defined in the request uri's query string.
        /// </summary>
        public IDictionary<string, string> QueryString
        {
            get { return queryString ?? (queryString = this.GetQueryString()); }
        }

        #endregion

        public KayakServerRequest(HttpRequestLine requestLine, IDictionary<string, string> headers, RequestStream body)
        {
            this.requestLine = requestLine;
            Headers = headers;
            Body = body;
        }
    }
}
