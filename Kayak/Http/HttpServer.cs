using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Kayak.Http
{
    public static class HttpServerExtensions
    {
        public static IServer CreateHttp(this IServerFactory factory, IHttpRequestDelegate channel, IScheduler scheduler)
        {
            var f = new HttpServerFactory(factory);
            return f.Create(channel, scheduler);
        }
    }

    class HttpServerFactory : IHttpServerFactory
    {
        IServerFactory serverFactory;

        public HttpServerFactory(IServerFactory serverFactory)
        {
            this.serverFactory = serverFactory;
        }

        public IServer Create(IHttpRequestDelegate del, IScheduler scheduler)
        {
            return serverFactory.Create(new HttpServerDelegate(del), scheduler);
        }
    }

    class HttpServerDelegate : IServerDelegate
    {
        IHttpRequestDelegate requestDelegate;

        public HttpServerDelegate(IHttpRequestDelegate requestDelegate)
        {
            this.requestDelegate = requestDelegate;
        }

        public ISocketDelegate OnConnection(IServer server, ISocket socket)
        {
            // XXX freelist
            var tx = new HttpServerTransaction(socket);
            var txDel = new HttpServerTransactionDelegate(requestDelegate, new OutputQueue(tx));
            var socketDelegate = new HttpServerSocketDelegate(tx, txDel);
            return socketDelegate;
        }

        public void OnClose(IServer server)
        {

        }
    }
}
