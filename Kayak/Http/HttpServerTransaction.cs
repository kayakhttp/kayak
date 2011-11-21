using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;

namespace Kayak.Http
{
    static class TransactionExtensions
    {
        public static void OnContinue(this IHttpServerTransaction transaction)
        {
            // write HTTP/1.1 100 Continue
            transaction.OnResponse(new HttpResponseHead() { Status = "100 Continue" });
            transaction.OnResponseEnd();
        }
    }

    class HttpServerTransaction : IHttpServerTransaction
    {
        ISocket socket;
        static readonly IHeaderRenderer defaultRenderer = new HttpResponseHeaderRenderer();
        IHeaderRenderer renderer = defaultRenderer;

        public HttpServerTransaction(ISocket socket)
        {
            this.socket = socket;
        }

        public IPEndPoint RemoteEndPoint
        {
            get { return socket.RemoteEndPoint; }
        }

        public void OnResponse(HttpResponseHead response)
        {
            renderer.Render(socket, response);
        }

        public bool OnResponseData(ArraySegment<byte> data, Action continuation)
        {
            return socket.Write(data, continuation);
        }

        public void OnResponseEnd()
        {
        }

        public void OnEnd()
        {
            socket.End();
        }

        public void Dispose()
        {
            socket.Dispose();
        }
    }

}
