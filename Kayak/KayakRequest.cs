using System.IO;

namespace Kayak
{
    public interface IKayakServerRequest
    {
        string Verb { get; }
        string RequestUri { get; }
        string HttpVersion { get; }
        NameValueDictionary Headers { get; }
        Stream Body { get; }
    }

    public class KayakServerRequest : IKayakServerRequest
    {
        HttpRequestLine requestLine;
        public string Verb { get { return requestLine.Verb; } }
        public string RequestUri { get { return requestLine.RequestUri; } }
        public string HttpVersion { get { return requestLine.HttpVersion; } }

        public NameValueDictionary Headers { get; private set; }
        public Stream Body { get; private set; }

        internal KayakServerRequest(HttpRequestLine requestLine, NameValueDictionary headers, Stream body)
        {
            this.requestLine = requestLine;
            Headers = headers;
            Body = body;
        }
    }
}
