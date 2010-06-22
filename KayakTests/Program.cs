using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using Kayak;

namespace KayakTests
{
    class Program
    {
        static byte[] responseBody;

        public static void Main(string[] args)
        {
            // make a canned response.
            var responseString = "";
            foreach (var i in Enumerable.Range(0, 100))
                responseString += "Canned response from Kayak.\r\n";
            responseBody = Encoding.UTF8.GetBytes(responseString);

            // construct a simple listener which throws off connections (implemented using System.Net.Socket)
            var listener = new SimpleListener(new IPEndPoint(IPAddress.Any, 8080));

            // construct a server to consume the connections
            var server = new KayakServer(listener);

            // server throws off contexts, kick off a coroutine to handle each.
            server.Subscribe<IKayakContext>(c => ProcessContext(c).AsCoroutine().Start());

            // let the server do its thing...
            Console.WriteLine("Press enter to exit.");
            Console.ReadLine();
        }

        static IEnumerable<object> ProcessContext(IKayakContext context)
        {
            context.Response.Headers["Server"] = "Kayak";
            // kick off an asynchronous write operation.
            yield return context.Response.Body.WriteAsync(responseBody, 0, responseBody.Length);
            // all done!
            context.End();
        }
    }
}
