using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Kayak.Core
{
    public interface IHttpResponder
    {
        // this makes the division of responsibility between the server and the application very clear.
        IObservable<IHttpServerResponse> Respond(IHttpServerRequest request, IDictionary<object, object> context);

        // the old interface encapsulates the request state better.
        //
        // but we could implement the old method with this. and it might
        // iron out some of the wrinkles.
        //
        // then the server is it's own thing as well. kayak, oars, whatever.
        // 
        // and actually, yeah, this is easier for server implementor probably than the 
        // whole socket/context thing. maybe?
        // so it's like,

        // kayak framework
        // nack | kayak context api 
        // simple implementation | oars
        // 
    }

    public class ResponderChain : IHttpResponder
    {
        IEnumerable<IHttpResponder> responders;

        public ResponderChain(IEnumerable<IHttpResponder> responders)
        {
            this.responders = responders;
        }

        public IObservable<IHttpServerResponse> Respond(IHttpServerRequest request, IDictionary<object, object> context)
        {
            foreach (var r in responders)
            {
                var result = r.Respond(request, context);

                if (result != null)
                    return result;
            }

            return null;
        }
    }

    public static partial class Extensions
    {
        public static IHttpResponder Chain(this IEnumerable<IHttpResponder> responders)
        {
            return new ResponderChain(responders);
        }
    }

    public interface IHttpServerRequest
    {
        HttpRequestLine RequestLine { get; }
        IDictionary<string, string> Headers { get; }
        IObservable<ArraySegment<byte>> GetBodyChunk();
    }

    public interface IHttpServerResponse
    {
        HttpStatusLine StatusLine { get; }
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
