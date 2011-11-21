using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;

namespace Kayak.Http
{
    public struct HttpRequestHead
    {
        public string Method;
        public string Uri;
        public string Path;
        public string QueryString;
        public string Fragment;
        public Version Version;
        public IDictionary<string, string> Headers;

        public override string ToString()
        {
            return string.Format("{0} {1} {2}\r\n{3}\r\n", Method, Uri, Version, 
                Headers != null 
                    ? Headers.Aggregate("", (acc, kv) => acc += string.Format("{0}: {1}\r\n", kv.Key, kv.Value))
                    : "");
        }
    }

    public struct HttpResponseHead
    {
        public string Status;
        public IDictionary<string, string> Headers;
    }

    public interface IHttpServerFactory
    {
        IServer Create(IHttpRequestDelegate del, IScheduler scheduler);
    }

    public interface IHttpRequestDelegate
    {
        void OnRequest(HttpRequestHead head, IDataProducer body, IHttpResponseDelegate response);
    }

    public interface IHttpResponseDelegate
    {
        void OnResponse(HttpResponseHead head, IDataProducer body);
    }

    interface IHttpServerTransaction : IDisposable
    {
        IPEndPoint RemoteEndPoint { get; }
        void OnResponse(HttpResponseHead response);
        bool OnResponseData(ArraySegment<byte> data, Action continuation);
        void OnResponseEnd();
        void OnEnd();
    }

    interface IHttpServerTransactionDelegate
    {
        void OnRequest(IHttpServerTransaction transaction, HttpRequestHead request, bool shouldKeepAlive);
        bool OnRequestData(IHttpServerTransaction transaction, ArraySegment<byte> data, Action continuation);
        void OnRequestEnd(IHttpServerTransaction transaction);
        void OnError(IHttpServerTransaction transaction, Exception e);
        void OnEnd(IHttpServerTransaction transaction);
        void OnClose(IHttpServerTransaction transaction);
    }
}
