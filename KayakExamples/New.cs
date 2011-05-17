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
    class NewSimple
    {
        public void Run()
        {
            // oars or kayak
            IScheduler scheduler = new KayakScheduler();

            scheduler.Post(() =>
                {
                    // finds the user's request delegate and serves it up (hot swap anyone?)
                    var serverDelegate = new HttpServerDelegate();

                    // server <- (request_delegate <- (), scheduler <- ())
                    IServer server = KayakServer.Factory.CreateHttp(serverDelegate, scheduler);

                    var unbind = server.Listen(new IPEndPoint(IPAddress.Any, 8080));

                    // XXX ought to wait for pending connections.
                    Exit.Listen(scheduler, () => { 
                        unbind.Dispose(); // server ought to automatically dispose all open sockets
                        scheduler.Stop(); 
                    });
                });

            // runs on calling thread
            scheduler.Start(new SchedulerDelegate());
        }

        static class Exit
        {
            public static void Listen(IScheduler scheduler, Action fired)
            {
                var thread = new Thread(() =>
                {
                    // would be nice to wait for cmd-c/kill
                    Console.WriteLine("Press enter to exit.");
                    Console.ReadLine();
                    scheduler.Post(fired);
                });

                thread.Start();
            }
        }

        class SchedulerDelegate : ISchedulerDelegate
        {
            public void OnException(IScheduler scheduler, Exception e)
            {
                Debug.WriteLine("Error on scheduler.");
                e.DebugStacktrace();
            }
        }

        class HttpServerDelegate : IHttpChannel
        {
            public void OnRequest(HttpRequestHead request, IDataProducer requestBody, 
                IHttpResponseDelegate response)
            {
                Debug.WriteLine("OnRequest");

                // hmmm...
                if (request.Uri == "/")
                {
                    response.OnResponse(new HttpResponseHead()
                    {
                        Status = "200 OK",
                        Headers = new Dictionary<string, string>() 
                        {
                            { "Content-Type", "text/plain" },
                            { "Content-Length", "20" },
                        }
                    },
                    new StringBody("Hello world.\r\nHello."));

                    Debug.WriteLine("OnRequest (hello): Ended response.");
                }
                else if (request.Uri == "/echo")
                {
                    response.OnResponse(new HttpResponseHead()
                    {
                        Status = "200 OK",
                        Headers = new Dictionary<string, string>() 
                        {
                            { "Content-Type", "text/plain" },
                            { "Connection", "close" }
                        }
                    }, requestBody);
                }
            }
        }

        class StringBody : IDataProducer
        {
            string body;
            public StringBody(string body)
            {
                this.body = body;
            }

            public IDisposable Connect(IDataConsumer channel)
            {
                channel.OnData(new ArraySegment<byte>(Encoding.ASCII.GetBytes(body)), null);
                channel.OnEnd();

                return null;
            }
        }
    }
}
