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
    public delegate void
        OwinApplication(IDictionary<string, object> env,
        Action<string, IDictionary<string, string>, IEnumerable<object>> respond,
        Action<Exception> error); 

    class Simple
    {
        class SimpleRequestDelegate : IRequestDelegate
        {
            IResponse response;

            public void OnStart(IRequest request, IResponse response)
            {
                Debug.WriteLine("OnStart");
                this.response = response;

                if (request.GetIsContinueExpected())
                    response.WriteContinue();

                response.WriteHeaders("200 OK",
                                new Dictionary<string, string>() 
                                {
                                    { "Content-Type", "text/plain" },
                                    { "Content-Length", "24" },
                                });
                response.WriteBody(new ArraySegment<byte>(Encoding.ASCII.GetBytes("Hello world.")), null);
                response.WriteBody(new ArraySegment<byte>(Encoding.ASCII.GetBytes("Hello world.")), null);
                response.End();
            }

            public bool OnBody(IRequest request, ArraySegment<byte> data, Action continuation)
            {
                Debug.WriteLine("OnBody");
                return false;
            }

            public void OnError(IRequest request, Exception e)
            {
            }

            public void OnEnd(IRequest request)
            {
                Debug.WriteLine("OnEnd");
            }
        }

        public static void Run2()
        {
            var server = new KayakServer();
            server.AcceptHttp(() => new SimpleRequestDelegate());
            server.Listen(new IPEndPoint(IPAddress.Any, 8080));

            Console.WriteLine("Listening on " + server.ListenEndPoint);
            Console.WriteLine("Press enter to exit.");
            Console.ReadLine();

            //pipe.Dispose();
        }




        //public static void Run()
        //{
        //    var server = new DotNetServer();

        //    var pipe = server.Start();

        //    server.Host((env, respond, error) =>
        //        {
        //            var path = env["Owin.RequestUri"] as string;

        //            if (path == "/")
        //                respond(
        //                        "200 OK",
        //                        new Dictionary<string, string>() 
        //                        {
        //                            { "Content-Type", "text/plain" }
        //                        },
        //                        new object[] { Encoding.ASCII.GetBytes("Hello world.") }
        //                    );
        //            else if (path == "/stream")
        //            {
        //                StreamingApp(env, respond, error);
        //            }
        //            else if (path == "/bufferedecho")
        //            {
        //                BufferedEcho(env, respond, error);
        //            }

        //        });

        //    Console.WriteLine("Listening on " + server.ListenEndPoint);
        //    Console.WriteLine("Press enter to exit.");
        //    Console.ReadLine();

        //    pipe.Dispose();
        //}

        //public static void StreamingApp(IDictionary<string, object> env,
        //    Action<string, IDictionary<string, string>, IEnumerable<object>> respond,
        //    Action<Exception> error)
        //{
        //    respond(
        //        "200 OK",
        //        new Dictionary<string, string>()
        //        {
        //            { "Content-Type", "text/plain" }
        //        },
        //        StreamingResponseBodyGenerator());
        //}

        //public static IEnumerable<object> StreamingResponseBodyGenerator()
        //{
        //    for (int i = 0; i < 10; i++)
        //    {
        //        Action<Action<object>, Action<Exception>> continuation =
        //        (result, exception) =>
        //        {
        //            // note: just using task because it's easy. could be anything, as long
        //            // as eventually you call either the result or exception callback.

        //            var asyncData = new Task<byte[]>(() =>
        //            {
        //                Thread.Sleep(1000);
        //                return Encoding.ASCII.GetBytes("This is chunk # " + i + "\r\n");
        //            });

        //            asyncData.ContinueWith(t =>
        //            {
        //                try
        //                {
        //                    result(t.Result);
        //                }
        //                catch (Exception e)
        //                {
        //                    exception(e);
        //                }
        //            });
        //            asyncData.Start();
        //        };
        //        yield return continuation;
        //    }
        //}

        //public static void BufferedEcho(IDictionary<string, object> env,
        //    Action<string, IDictionary<string, string>, IEnumerable<object>> respond,
        //    Action<Exception> error)
        //{
        //    var requestBody = env["Owin.RequestBody"] as Action<byte[], int, int, Action<int>, Action<Exception>>;
        //    var contentLength = int.Parse(((IDictionary<string, string>)env["Owin.RequestHeaders"])["Content-Length"]);
        //    var buffer = new byte[1024];
        //    var sb = new StringBuilder();

        //    BufferRequestBody(requestBody, buffer, sb, 0, contentLength)(
        //        result => respond("200 OK",
        //                    new Dictionary<string, string>()
        //                    {
        //                        { "Content-Type", "text/plain" },
        //                        { "Content-Length", result.Length.ToString() }
        //                    },
        //                    new object[] { Encoding.UTF8.GetBytes(sb.ToString()) }), 
        //        exception => error(exception));
        //}

        //static Action<Action<string>, Action<Exception>> BufferRequestBody(Action<byte[], int, int, Action<int>, Action<Exception>> requestBody, byte[] buffer, StringBuilder sb, int totalBytesRead, int contentLength)
        //{
        //    return (r, e) =>
        //        requestBody(buffer, 0, buffer.Length, bytesRead =>
        //        {
        //            if (bytesRead > 0)
        //            {
        //                sb.Append(Encoding.UTF8.GetString(buffer, 0, bytesRead));
        //                totalBytesRead += bytesRead;
        //            }

        //            if (totalBytesRead == contentLength)
        //                r(sb.ToString());
        //            else
        //            {
        //                BufferRequestBody(requestBody, buffer, sb, totalBytesRead, contentLength)(r, e);
        //            }
        //        },
        //        exception =>
        //        {
        //            e(new Exception("Error reading request body."));
        //        });
        //}

    }
}
