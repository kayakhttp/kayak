using System;
using System.Collections.Generic;

namespace Kayak.Http
{
    public interface IRequestDelegate
    {
        void OnStart(IRequest request, IResponse response);
        bool OnBody(ArraySegment<byte> data, Action continuation);
        void OnError(Exception exception);
        void OnEnd();
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
