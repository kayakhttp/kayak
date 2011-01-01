Kayak is a lightweight C# web server featuring native OWIN support.

Kayak is Copyright (c) 2010 Benjamin van der Veen. Kayak is licensed under the 
MIT License. See LICENSE.txt.

[http://kayakhttp.com](http://kayakhttp.com)
[http://bvanderveen.com](http://bvanderveen.com)

# Example

To run an OWIN app:

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