using System;
using System.Collections.Generic;

namespace Kayak.Http
{
    public class HttpRequestEventArgs : EventArgs
    {
        public IRequest Request { get; internal set; }
        public IResponse Response { get; internal set; }
    }

    public interface IHttpServer
    {
        event EventHandler<HttpRequestEventArgs> OnRequest;
    }

    public interface IHttpServerDelegate
    {
        void OnRequest(IRequest request, IResponse response);
        //void OnUpgrade(IRequest request, ISocket socket, ArraySegment<byte> head);
    }

    public interface IRequest
    {
        event EventHandler<DataEventArgs> OnBody;
        event EventHandler OnEnd;

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
