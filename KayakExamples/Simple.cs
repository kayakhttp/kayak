using System;
using System.Collections.Generic;
using System.Text;
using Kayak;

namespace KayakExamples
{
    public delegate void
        OwinApplication(IDictionary<string, object> env,
        Action<string, IDictionary<string, IList<string>>, IEnumerable<object>> completed,
        Action<Exception> faulted); 

    class Simple
    {
        public static void Run()
        {
            var server = new DotNetServer();

            var pipe = server.Start();

            server.Host((env, respond, error) =>
                {
                    respond(
                            "200 OK",
                            new Dictionary<string, IList<string>>() 
                            {
                                { "Content-Type",  new string[] { "text/html" } }
                            },
                            new object[] { Encoding.ASCII.GetBytes("Hello world.") }
                        );
                });

            Console.WriteLine("Listening on " + server.ListenEndPoint);
            Console.WriteLine("Press enter to exit.");
            Console.ReadLine();

            pipe.Dispose();
        }
    }
}
