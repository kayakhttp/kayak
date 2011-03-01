using System;
using System.Collections.Generic;

namespace Kayak.Http
{
    public interface IRequestDelegate
    {
        void OnStart(IRequest request, IResponse response);
        void OnBody(IRequest request, ArraySegment<byte> data, Action continuation);
        void OnError(IRequest request, Exception exception);
        void OnEnd(IRequest request);
    }

    public interface IRequest
    {
        string Method { get; }
        string Uri { get; }
        Version Version { get; }
        IDictionary<string, string> Headers { get; }
    }

    public interface IResponse
    {
        void WriteContinue();
        void WriteHeaders(string status, IDictionary<string, string> headers);
        void WriteBody(ArraySegment<byte> data, Action continuation);
        void End();
    }
}
