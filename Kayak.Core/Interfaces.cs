using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Kayak.Core
{
    public interface IHttpResponder
    {
        IObservable<IHttpServerResponse> Respond(IHttpServerRequest request, IDictionary<object, object> context);
    }

    public interface IHttpServerRequest
    {
        HttpRequestLine RequestLine { get; }
        IDictionary<string, string> Headers { get; }
        IObservable<ArraySegment<byte>> GetBodyChunk();
    }

    public interface IHttpServerResponse
    {
        //HttpStatusLine StatusLine { get; }

        int StatusCode { get; }
        string ReasonPhrase { get; }
        string HttpVersion { get; }

        IDictionary<string, string> Headers { get; }
        string BodyFile { get; }
        IObservable<ArraySegment<byte>> GetBodyChunk();
    }

    public struct HttpStatusLine
    {
        public int StatusCode;
        public string ReasonPhrase;
        public string HttpVersion;
    }

    /// <summary>
    /// Represents the first line of an HTTP request. Used when constructing a `KayakServerRequest`.
    /// </summary>
    public struct HttpRequestLine
    {
        /// <summary>
        /// The verb component of the request line (e.g., GET, POST, etc).
        /// </summary>
        public string Verb;
        /// <summary>
        /// The request URI component of the request line (e.g., /path/and?query=string).
        /// </summary>
        public string RequestUri;

        /// <summary>
        /// The HTTP version component of the request line (e.g., HTTP/1.0).
        /// </summary>
        public string HttpVersion;
    }
}
