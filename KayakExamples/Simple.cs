using System;
using System.Collections.Generic;
using System.Text;
using Kayak;
using Kayak.Http;
using System.Threading.Tasks;
using System.Threading;
using System.Diagnostics;
using System.Net;

namespace KayakExamples
{
    class Simple
    {
        class EchoResponse
        {
            IHttpServerResponse response;

            public EchoResponse(IHttpServerRequest request, IHttpServerResponse response)
            {
                request.OnBody += new EventHandler<DataEventArgs>(request_OnBody);
                request.OnEnd += new EventHandler(request_OnEnd);
                this.response = response;
            }

            void request_OnBody(object sender, DataEventArgs e)
            {
                e.WillInvokeContinuation = response.WriteBody(e.Data, e.Continuation);
            }

            void request_OnEnd(object sender, EventArgs e)
            {
                response.End();
            }
        }

        static IScheduler scheduler;

        public static void Run2()
        {
            scheduler = new KayakScheduler();
            var server = new KayakServer(scheduler);

            var http = server.AsHttpServer();
            http.OnRequest += OnRequest;

            server.Listen(new IPEndPoint(IPAddress.Any, 8080));

            scheduler.Start();
            server.OnClose += new EventHandler(server_OnClose);

            Console.WriteLine("Listening on " + server.ListenEndPoint);
            Console.WriteLine("Press enter to exit.");
            Console.ReadLine();

            http.OnRequest -= OnRequest;
            server.Close();
        }

        static void server_OnClose(object sender, EventArgs e)
        {
            scheduler.Stop();
        }

        static void OnRequest(object sender, HttpRequestEventArgs e)
        {
            var request = e.Request;
            var response = e.Response;

            Debug.WriteLine("OnStart");

            if (request.Uri == "/")
            {
                response.WriteHeaders("200 OK",
                                new Dictionary<string, string>() 
                                {
                                    { "Content-Type", "text/plain" },
                                    { "Content-Length", "20" },
                                });
                response.WriteBody(new ArraySegment<byte>(Encoding.ASCII.GetBytes("Hello world.\r\nHello.")), null);
                response.End();
            }
            else if (request.Uri == "/echo")
            {
                response.WriteHeaders("200 OK",
                    new Dictionary<string, string>()
                        {
                            { "Content-Type", "text/plain" },
                            { "Connection", "close" }
                        });

                if (request.IsContinueExpected())
                    response.WriteContinue();

                new EchoResponse(request, response);
            }

        }



    }
}
