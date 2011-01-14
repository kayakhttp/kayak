using System;
using System.Collections.Generic;
using System.Text;
using Kayak;
using System.Threading.Tasks;
using System.Threading;

namespace KayakExamples
{
    public delegate void
        OwinApplication(IDictionary<string, object> env,
        Action<string, IDictionary<string, IList<string>>, IEnumerable<object>> respond,
        Action<Exception> error); 

    class Simple
    {
        public static void Run()
        {
            var server = new DotNetServer();

            var pipe = server.Start();

            server.Host((env, respond, error) =>
                {
                    var path = env["Owin.RequestUri"] as string;

                    if (path == "/")
                        respond(
                                "200 OK",
                                new Dictionary<string, IList<string>>() 
                                {
                                    { "Content-Type",  new string[] { "text/plain" } }
                                },
                                new object[] { Encoding.ASCII.GetBytes("Hello world.") }
                            );
                    else if (path == "/stream")
                    {
                        StreamingApp(env, respond, error);
                    }
                    else if (path == "/bufferedecho")
                    {
                        BufferedEcho(env, respond, error);
                    }

                });

            Console.WriteLine("Listening on " + server.ListenEndPoint);
            Console.WriteLine("Press enter to exit.");
            Console.ReadLine();

            pipe.Dispose();
        }

        public static void StreamingApp(IDictionary<string, object> env,
            Action<string, IDictionary<string, IList<string>>, IEnumerable<object>> respond,
            Action<Exception> error)
        {
            respond(
                "200 OK",
                new Dictionary<string, IList<string>>()
                {
                    { "Content-Type", new string[] { "text/plain" } }
                },
                StreamingResponseBodyGenerator());
        }

        public static IEnumerable<object> StreamingResponseBodyGenerator()
        {
            for (int i = 0; i < 10; i++)
            {
                Action<Action<object>, Action<Exception>> continuation =
                (result, exception) =>
                {
                    // note: just using task because it's easy. could be anything, as long
                    // as eventually you call either the result or exception callback.

                    var asyncData = new Task<byte[]>(() =>
                    {
                        Thread.Sleep(1000);
                        return Encoding.ASCII.GetBytes("This is chunk # " + i + "\r\n");
                    });

                    asyncData.ContinueWith(t =>
                    {
                        try
                        {
                            result(t.Result);
                        }
                        catch (Exception e)
                        {
                            exception(e);
                        }
                    });
                    asyncData.Start();
                };
                yield return continuation;
            }
        }

        public static void BufferedEcho(IDictionary<string, object> env,
            Action<string, IDictionary<string, IList<string>>, IEnumerable<object>> respond,
            Action<Exception> error)
        {
            var requestBody = env["Owin.RequestBody"] as Action<byte[], int, int, Action<int>, Action<Exception>>;
            var contentLength = int.Parse(((IDictionary<string, IList<string>>)env["Owin.RequestHeaders"])["Content-Length"][0]);
            var buffer = new byte[1024];
            var sb = new StringBuilder();

            BufferRequestBody(requestBody, buffer, sb, 0, contentLength)(
                result => respond("200 OK",
                            new Dictionary<string, IList<string>>()
                            {
                                { "Content-Type", new string[] { "text/plain" } },
                                { "Content-Length", new string[] { result.Length.ToString() } }
                            },
                            new object[] { Encoding.UTF8.GetBytes(sb.ToString()) }), 
                exception => error(exception));
        }

        static Action<Action<string>, Action<Exception>> BufferRequestBody(Action<byte[], int, int, Action<int>, Action<Exception>> requestBody, byte[] buffer, StringBuilder sb, int totalBytesRead, int contentLength)
        {
            return (r, e) =>
                requestBody(buffer, 0, buffer.Length, bytesRead =>
                {
                    if (bytesRead > 0)
                    {
                        sb.Append(Encoding.UTF8.GetString(buffer, 0, bytesRead));
                        totalBytesRead += bytesRead;
                    }

                    if (totalBytesRead == contentLength)
                        r(sb.ToString());
                    else
                    {
                        BufferRequestBody(requestBody, buffer, sb, totalBytesRead, contentLength)(r, e);
                    }
                },
                exception =>
                {
                    e(new Exception("Error reading request body."));
                });
        }

    }
}
