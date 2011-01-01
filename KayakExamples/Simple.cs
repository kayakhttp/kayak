using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using Kayak;

namespace KayakExamples
{
    class Simple
    {
        public static void Run()
        {
            var server = new DotNetServer();

            var pipe = server.Start();

            server.Host((env, respond, error) =>
                {
                    respond(new Tuple<string, IDictionary<string, IEnumerable<string>>, IEnumerable<object>>(
                            "200 OK",
                            new Dictionary<string, IEnumerable<string>>() 
                            {
                                { "Content-Type",  new string[] { "text/html" } }
                            },
                            new object[] { Encoding.ASCII.GetBytes("Hello world.") }
                        ));
                });

            Console.WriteLine("Listening on " + server.ListenEndPoint);
            Console.WriteLine("Press enter to exit.");
            Console.ReadLine();

            pipe.Dispose();
        }
    }
}
