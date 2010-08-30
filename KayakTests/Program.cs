using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using Kayak;
using Kayak.Framework;
using Kayak.Oars;
using System.Reflection;
using LitJson;
using System.IO;

namespace KayakTests
{
    class Program
    {
    //    static byte[] responseBody;

    //    public static void Main(string[] args)
    //    {
    //        // make a canned response.
    //        var responseString = "";
    //        foreach (var i in Enumerable.Range(0, 100))
    //            responseString += "Canned response from Kayak.\r\n";
    //        responseBody = Encoding.UTF8.GetBytes(responseString);

    //        // construct a simple listener which throws off connections (implemented using System.Net.Socket)
    //        //var listener = new SimpleListener(new IPEndPoint(IPAddress.Any, 8080));
    //        var listener = new OarsListener(new IPEndPoint(IPAddress.Any, 8080), 1000);

    //        // construct a server to consume the connections
    //        var server = new KayakServer(listener);

    //        // server throws off contexts, kick off a coroutine to handle each.
    //        server.Subscribe<IKayakContext>(
    //            c =>
    //            {
    //                ProcessContext(c).AsCoroutine().Start();
    //            }, 
    //            e =>
    //            {
    //                Console.WriteLine("Server error!");
    //                Console.Out.WriteException(e);
    //            }, () => { });

    //        listener.Start();

    //        Console.WriteLine("Press enter to exit.");
    //        Console.ReadLine();
    //        listener.Stop();
    //    }

    //    static int completed = 0;

    //    static IEnumerable<object> ProcessContext(IKayakContext context)
    //    {
    //        context.Response.Headers["Server"] = "Kayak";
    //        // kick off an asynchronous write operation.
    //        //Console.WriteLine("Test server: writing some bytes.");

    //        byte[] buffer = new byte[1024 * 2];
    //        int bytesRead = 0;
    //        do
    //        {
    //            //Console.WriteLine("will read.");
    //            yield return context.Request.Body.ReadAsync(buffer, 0, buffer.Length).Do(n => bytesRead = n);
    //            //Console.WriteLine("read " + bytesRead + " bytes");
    //            yield return context.Response.Body.WriteAsync(buffer, 0, bytesRead);
    //            //Console.WriteLine("Test server: wrote bytes!");
    //        }
    //        while (bytesRead != 0);
    //        // all done!
    //        context.End();
    //        //Console.WriteLine("Ended context " + ++completed);
    //    }


        class JsonExceptionHandler : IInvocationExceptionHandler
        {
            TypedJsonMapper mapper;

            public JsonExceptionHandler(TypedJsonMapper mapper)
            {
                this.mapper = mapper;
            }

            public IObservable<Unit> HandleException(IKayakContext context, InvocationInfo info, Exception exception)
            {
                if (exception is TargetInvocationException)
                    exception = exception.InnerException;
                return new { Error = exception.Message }.WriteJsonResponse(context, mapper);
            }
        }

        public static void Main(string[] args)
        {
            //var server = new OarsServer(new IPEndPoint(IPAddress.Any, 8080), 1000);
            var server = new KayakServer();

            var behavior = new KayakInvocationBehavior();

            // use the mapping functionality which searches for methods marked with [Path]
            behavior.MapTypes(Assembly.GetExecutingAssembly().GetTypes());

            var mapper = new TypedJsonMapper();
            mapper.AddDefaultOutputConversions();
            mapper.AddDefaultInputConversions();

            behavior.AddFileSupport();
            behavior.AddJsonSupport(mapper);

            behavior.ExceptionHandlers.Clear();
            behavior.ExceptionHandlers.Add(new JsonExceptionHandler(mapper));

            Func<IKayakContext, bool> shouldBeHandledByCustomCode = c => c.Request.Path.StartsWith("/data");

            server.UseFramework(behavior);
            //var split = server.ToContexts().Split(
            //    (c, custom, framework) => { 
            //        if (shouldBeHandledByCustomCode(c)) 
            //            custom.OnNext(c); 
            //        else 
            //            framework.OnNext(c); 
            //    });

            //var contextsForFramework = split[0];
            //var contextsForCustomCode = split[1];

            //contextsForFramework.UseFramework();
            //contextsForCustomCode.Subscribe(c => { });

            Console.ReadLine();
        }
    }

    public class MyService : KayakService
    {
        [Path("/")]
        public object Test(string param)
        {
            return new { greeting = "hello.", param = param };
        }

        static object test;

        [Verb("POST")]
        [Verb("PUT")]
        [Path("/test")]
        public void PutTest([RequestBody] object t)
        {
            test = t;
        }

        [Path("/test")]
        public object GetTest()
        {
            return test;
        }

        [Path("/error")]
        public void Error()
        {
            throw new Exception("Uh oh.");
        }

        [Path("/path/{p}")]
        public object PathParam(string p)
        {
            return new { p_was = p };
        }

        [Path("/files/{name}")]
        public FileInfo GetFile(string name)
        {
            return new FileInfo(name);
        }
    }
}
