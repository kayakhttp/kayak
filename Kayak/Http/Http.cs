using System;
using System.Collections.Generic;

namespace Kayak.Http
{
    public struct HttpRequestHead
    {
        public string Method;
        public string Uri;
        public Version Version;
        public IDictionary<string, string> Headers;
    }

    public struct HttpResponseHead
    {
        public string Status;
        public IDictionary<string, string> Headers;
    }

    public interface IHttpServerFactory
    {
        IServer Create(IHttpServerDelegate del, IScheduler scheduler);
    }

    public interface IHttpClientFactory
    {
        IHttpRequest Create(IHttpResponseDelegate del);
    }

    public interface IHttpServerDelegate
    {
        IHttpRequestDelegate OnRequest(IServer server, IHttpResponse response);
        void OnClose(IServer server);
    }

    public interface IHttpRequest
    {
        void WriteHeaders(HttpRequestHead head);
        bool WriteBody(ArraySegment<byte> data, Action continuation);
        void End();
    }

    public interface IHttpResponseDelegate
    {
        void OnContinue();
        void OnHeaders(HttpResponseHead head);
        bool OnBody(ArraySegment<byte> data, Action continuation);
        void OnEnd();
    }

    public interface IHttpRequestDelegate
    {
        void OnHeaders(HttpRequestHead head);
        bool OnBody(ArraySegment<byte> data, Action continuation);
        void OnEnd();
    }

    public interface IHttpResponse
    {
        void WriteContinue();
        void WriteHeaders(HttpResponseHead head);
        bool WriteBody(ArraySegment<byte> data, Action continuation);
        void End();
    }
}
