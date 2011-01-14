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
    }
}
