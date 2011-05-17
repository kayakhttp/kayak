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
        IServer Create(IHttpChannel del, IScheduler scheduler);
    }

    public interface IHttpChannel
    {
        void OnRequest(HttpRequestHead request, IDataProducer requestBody, IHttpResponseDelegate response);
    }

    public interface IHttpResponseDelegate
    {
        void OnResponse(HttpResponseHead head, IDataProducer body);
    }
}
