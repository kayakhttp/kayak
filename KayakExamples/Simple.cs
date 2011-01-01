using System;
using System.Collections.Generic;
using Kayak;
using Owin;
using System.Text;
using System.Net;

namespace KayakExamples
{
    class Simple
    {
        public static void Run()
        {
            var server = new KayakServer2(new IPEndPoint(IPAddress.Any, 8080), 50);

            server.Start();

            Console.WriteLine("Listening on " + server.ListenEndPoint);

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
            Console.WriteLine("Press enter to exit.");
            Console.ReadLine();
            server.Stop();
        }

        //static IResponse Respond(IRequest request)
        //{
        //    // note that attempting to read the request body
        //    // would be a bad idea, since that's
        //    // an asynchronous operation and this func must
        //    // return response immediately. threadpool
        //    // stavation risk.

        //    return new KayakResponse(
        //        "200 OK",
        //        new Dictionary<string, IEnumerable<string>>() 
        //        {
        //            { "Content-Type",  new string[] { "text/html" } }
        //        },
        //        "Hello world.");
        //}
    }

    //class SimpleApp : IApplication
    //{
    //    Func<IRequest, IResponse> respond;

    //    public SimpleApp(Func<IRequest, IResponse> respond)
    //    {
    //        this.respond = respond;
    //    }

    //    public IAsyncResult BeginInvoke(IRequest request, AsyncCallback callback, object state)
    //    {
    //        return respond.BeginInvoke(request, callback, state);
    //    }

    //    public IResponse EndInvoke(IAsyncResult result)
    //    {
    //        return respond.EndInvoke(result);
    //    }
    //}
}
