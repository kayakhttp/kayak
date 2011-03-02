using System;
using System.Collections.Generic;

namespace Kayak.Http
{
    public interface IHttpServerDelegate
    {
        void OnRequest(IRequest request, IResponse response);
        //void OnUpgrade(IRequest request, ISocket socket, ArraySegment<byte> head);
    }

    public interface IRequestDelegate
    {
        bool OnBody(ArraySegment<byte> data, Action continuation);
        void OnEnd();
    }

    public interface IRequest
    {
        IRequestDelegate Delegate { get; set; }

        string Method { get; }
        string Uri { get; }
        Version Version { get; }
        IDictionary<string, string> Headers { get; }
    }

    public interface IResponse
    {
        void WriteContinue();
        void WriteHeaders(string status, IDictionary<string, string> headers);
        bool WriteBody(ArraySegment<byte> data, Action continuation);
        void End();
    }
}
