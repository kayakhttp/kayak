using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Kayak;
using System.Net;
using Kayak.Http;
using System.Threading;
using System.Diagnostics;

namespace KayakExamples
{
    class NewSimple : ISchedulerDelegate, IHttpServerDelegate
    {
        ManualResetEventSlim wh;
        IDisposable unbind;
        Action stopScheduler;

        public void Run()
        {
            wh = new ManualResetEventSlim();

            ISchedulerFactory schedulerFactory = null;
            var scheduler = schedulerFactory.Create(this);
            var stopper = scheduler.Start();
            stopScheduler = () => { stopper.Dispose(); };
            Console.ReadLine();
            scheduler.Post(() => { unbind.Dispose(); });

            wh.Wait();
            wh.Dispose();
        }

        public void OnStarted(IScheduler scheduler)
        {
            IHttpServerFactory httpServerFactory = null;
            IServer server = httpServerFactory.Create(this, scheduler);
            server.Listen(new IPEndPoint(IPAddress.Any, 8080));
        }

        public void OnStopped(IScheduler scheduler)
        {
            wh.Set();
        }

        public void OnException(IScheduler scheduler, Exception e)
        {
            Debug.WriteLine("Error on scheduler.");
            e.PrintStacktrace();
        }

        public IHttpRequestDelegate OnRequest(IServer server, IHttpResponse response)
        {
            return new RequestDelegate() { Response = response };
        }

        class RequestDelegate : IHttpRequestDelegate
        {
            public IHttpResponse Response { get; set; }

            IHttpRequestDelegate del;

            public RequestDelegate() { }

            public void OnHeaders(HttpRequestHead request)
            {
                Debug.WriteLine("OnRequest");

                if (request.Uri == "/")
                {
                    del = new HelloDelegate() { Response = Response };
                }
                else if (request.Uri == "/echo")
                {
                    del = new EchoDelegate() { Response = Response };
                }
            }

            public bool OnBody(ArraySegment<byte> data, Action continuation)
            {
                return del.OnBody(data, continuation);
            }

            public void OnEnd()
            {
                del.OnEnd();
            }

            class HelloDelegate : IHttpRequestDelegate
            {
                public IHttpResponse Response { get; set; }

                public void OnHeaders(HttpRequestHead head)
                {
                    Response.WriteHeaders(new HttpResponseHead()
                    {
                        Status = "200 OK",
                        Headers = new Dictionary<string, string>() 
                        {
                            { "Content-Type", "text/plain" },
                            { "Content-Length", "20" },
                        }
                    });
                    Response.WriteBody(new ArraySegment<byte>(Encoding.ASCII.GetBytes("Hello world.\r\nHello.")), null);
                    Response.End();
                    Debug.WriteLine("HelloDelegate.OnHeaders: Ended response.");
                }

                public bool OnBody(ArraySegment<byte> data, Action continuation) { return false; }
                public void OnEnd() { }
            }

            class EchoDelegate : IHttpRequestDelegate
            {
                public IHttpResponse Response { get; set; }

                public void OnHeaders(HttpRequestHead request)
                {
                    if (request.IsContinueExpected())
                        Response.WriteContinue();

                    Response.WriteHeaders(new HttpResponseHead()
                    {
                        Status = "200 OK",
                        Headers = new Dictionary<string, string>() 
                        {
                            { "Content-Type", "text/plain" },
                            { "Connection", "close" }
                        }
                    });
                }

                public bool OnBody(ArraySegment<byte> data, Action continuation)
                {
                    return Response.WriteBody(data, continuation);
                }

                public void OnEnd()
                {
                    Response.End();
                }
            }

        }

        public void OnClose(IServer server)
        {
            // ideally we wait until all connections are closed. XXX how?
            stopScheduler();
        }
    }
}
