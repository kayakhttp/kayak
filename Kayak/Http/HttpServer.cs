using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Kayak.Http
{
    public static class HttpServerExtensions
    {
        public static IServer CreateHttp(this IServerFactory factory, IHttpChannel channel)
        {
            return CreateHttp(factory, channel, KayakScheduler.Current as KayakScheduler);
        }

        public static IServer CreateHttp(this IServerFactory factory, IHttpChannel channel, IScheduler scheduler)
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

        public IServer Create(IHttpChannel del, IScheduler scheduler)
        {
            return serverFactory.Create(new HttpServerDelegate(del), scheduler);
        }
    }

    class HttpServerDelegate : IServerDelegate
    {
        IResponseFactory responseFactory;

        public HttpServerDelegate(IHttpChannel channel)
        {
            this.responseFactory = new ResponseFactory(channel);
        }

        public ISocketDelegate OnConnection(IServer server, ISocket socket)
        {
            var txDel = new HttpServerTransactionDelegate(responseFactory);
            txDel.Subscribe(new OutputSegmentQueue(socket));
            return new HttpServerSocketDelegate(txDel);
        }

        public void OnClose(IServer server)
        {

        }
    }
}
