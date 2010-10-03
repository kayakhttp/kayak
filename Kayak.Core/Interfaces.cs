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
        string Verb { get; }
        string RequestUri { get; }
        string HttpVersion { get; }
        IDictionary<string, string> Headers { get; }

        IObservable<int> GetBodyChunk(byte[] buffer, int offset, int count);
    }

    public interface IHttpServerResponse
    {
        int StatusCode { get; }
        string ReasonPhrase { get; }
        string HttpVersion { get; }
        IDictionary<string, string> Headers { get; }

        string BodyFile { get; }
        IObservable<ArraySegment<byte>> GetBodyChunk();
    }
}
