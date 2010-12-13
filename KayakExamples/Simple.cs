using System;
using System.Collections.Generic;
using Kayak;
using Owin;
using System.Text;

namespace KayakExamples
{
    class Simple
    {
        public static void Run()
        {
            var server = new KayakServer();
            var pipe = server.Invoke(new SimpleApp(Respond));
            Console.WriteLine("Press enter to exit.");
            Console.ReadLine();
            pipe.Dispose();
        }

        static IResponse Respond(IRequest request)
        {
            // note that attempting to read the request body
            // would be a bad idea, since that's
            // an asynchronous operation and this func must
            // return response immediately. threadpool
            // stavation risk.

            return new KayakResponse(
                "200 OK",
                new Dictionary<string, IEnumerable<string>>() 
                {
                    { "Content-Type",  new string[] { "text/html" } }
                },
                "Hello world.");
        }
    }

    class SimpleApp : IApplication
    {
        Func<IRequest, IResponse> respond;

        public SimpleApp(Func<IRequest, IResponse> respond)
        {
            this.respond = respond;
        }

        public IAsyncResult BeginInvoke(IRequest request, AsyncCallback callback, object state)
        {
            return respond.BeginInvoke(request, callback, state);
        }

        public IResponse EndInvoke(IAsyncResult result)
        {
            return respond.EndInvoke(result);
        }
    }
}
