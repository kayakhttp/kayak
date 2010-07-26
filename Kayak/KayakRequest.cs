using System.IO;
using System.Web;

namespace Kayak
{
    public interface IKayakServerRequest
    {
        string Verb { get; }
        string RequestUri { get; }
        string HttpVersion { get; }
        NameValueDictionary Headers { get; }
        Stream Body { get; }

        #region Derived properties

        string Path { get; }
        NameValueDictionary QueryString { get; }

        #endregion
        //string this[string name];
    }

    public class KayakServerRequest : IKayakServerRequest
    {
        HttpRequestLine requestLine;
        string path;
        NameValueDictionary queryString;

        public string Verb { get { return requestLine.Verb; } }
        public string RequestUri { get { return requestLine.RequestUri; } }
        public string HttpVersion { get { return requestLine.HttpVersion; } }

        public NameValueDictionary Headers { get; private set; }
        public Stream Body { get; private set; }


        #region Derived properties

        /// <summary>
        /// Get the Uri Path for this request, i.e. "/some/path" without querystring or "http://".
        /// </summary>
        public string Path
        {
            get { return path ?? (path = BuildPath()); }
        }

        string BuildPath()
        {
            var question = RequestUri.IndexOf('?');
            return HttpUtility.UrlDecode(question >= 0 ? RequestUri.Substring(0, question) : RequestUri);
        }

        /// <summary>
        /// Gets the collection of parameters defined in the request uri's query string.
        /// </summary>
        public NameValueDictionary QueryString
        {
            get { return queryString ?? (queryString = BuildQueryString()); }
        }

        NameValueDictionary BuildQueryString()
        {
            int question = RequestUri.IndexOf('?');

            return question >= 0 ?
                RequestUri.DecodeQueryString(question + 1, RequestUri.Length - question - 1) :
                new NameValueDictionary();
        }

        #endregion

        internal KayakServerRequest(HttpRequestLine requestLine, NameValueDictionary headers, Stream body)
        {
            this.requestLine = requestLine;
            Headers = headers;
            Body = body;
        }
    }
}
