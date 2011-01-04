Kayak is a lightweight C# web server. Natively support the [OWIN](http://owin.github.com) specification.

Kayak is Copyright (c) 2010 Benjamin van der Veen. Kayak is licensed under the 
MIT License. See LICENSE.txt.

[http://kayakhttp.com](http://kayakhttp.com)<br>
[http://bvanderveen.com](http://bvanderveen.com)


# Example

To run an OWIN app:


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