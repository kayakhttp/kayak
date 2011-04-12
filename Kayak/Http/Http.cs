using System;
using System.Collections.Generic;

namespace Kayak.Http
{
    public class HttpRequestEventArgs : EventArgs
    {
        public IHttpServerRequest Request { get; internal set; }
        public IHttpServerResponse Response { get; internal set; }
    }

    public interface IHttpServer
    {
        event EventHandler<HttpRequestEventArgs> OnRequest;
    }

    public interface IHttpServerRequest
    {
        event EventHandler<DataEventArgs> OnBody;
        event EventHandler OnEnd;

        string Method { get; }
        string Uri { get; }
        Version Version { get; }
        IDictionary<string, string> Headers { get; }
    }

    public interface IHttpServerResponse
    {
        void WriteContinue();
        void WriteHeaders(string status, IDictionary<string, string> headers);
        bool WriteBody(ArraySegment<byte> data, Action continuation);
        void End();
    }
}
