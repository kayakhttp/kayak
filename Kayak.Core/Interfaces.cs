using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Kayak.Core
{
    public interface IHttpResponder
    {
        object Respond(IHttpServerRequest request, IDictionary<object, object> context);
    }

    public interface IHttpServerRequest
    {
        string Verb { get; }
        string RequestUri { get; }
        string HttpVersion { get; }
        IDictionary<string, string> Headers { get; }
        IEnumerable<Func<byte[], int, int, IObservable<int>>> GetBody();
    }

    public interface IHttpServerResponse
    {
        int StatusCode { get; }
        string ReasonPhrase { get; }
        string HttpVersion { get; }
        IDictionary<string, string> Headers { get; }
        string BodyFile { get; }
        IEnumerable<IObservable<ArraySegment<byte>>> GetBody();
    }
}
