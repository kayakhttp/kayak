using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Kayak;
using Kayak.Framework;
using Owin;

namespace KayakExamples
{
    class Framework
    {
        public static void Run()
        {
            var server = new KayakServer();
            var pipe = server.UseFramework();
            Console.WriteLine("Press enter to exit.");
            Console.ReadLine();
            pipe.Dispose();
        }

        public class ExampleService : KayakService
        {
            // you can return IResponse...
            [Path("/")]
            public IResponse Root()
            {
                return new KayakResponse("200 OK", "They speak English in What?");
            }

            // or an object to be serialized as json.
            // sets content-type and content-length automatically.
            // 200 OK for objects, 503 Internal Server Error for exceptions
            [Path("/hello")]
            public object SayHello(string name)
            {
                return "hello, " + name + ".";
            }

            static object foo;

            [Verb("POST")]
            [Verb("PUT")]
            [Path("/foo")]
            public IResponse PutFoo([RequestBody] object t)
            {
                foo = t;
                return new KayakResponse("201 Created");
            }

            [Path("/foo")]
            public object GetFoo()
            {
                return foo;
            }

            [Path("/error")]
            public object Error()
            {
                throw new Exception("Uh oh.");
            }

            [Path("/files/{name}")]
            public FileInfo GetFile(string name)
            {
                return new FileInfo(name);
            }

            [Verb("POST")]
            [Path("/postvars")]
            public object PostVars()
            {
                // TODO parse application/x-www-form-urlencoded
                return null;
            }

            [Verb("POST")]
            [Verb("PUT")]
            [Path("/echo")]
            public object Echo()
            {
                int contentLength = -1;

                if (Request.Headers.ContainsKey("Content-Length"))
                    contentLength = int.Parse(Request.Headers["Content-Length"].First());

                return new KayakResponse(
                    "200 OK",
                    new Dictionary<string, IEnumerable<string>>()
                {
                    { "Content-Length", new string[] { contentLength.ToString() } }
                },
                    EchoGenerator(Request));
            }

            public static IEnumerable<object> EchoGenerator(IRequest request)
            {
                var buffer = new byte[2048];

                var bytesRead = 0;
                do
                {
                    yield return Observable.CreateWithDisposable<ArraySegment<byte>>(o =>
                    {
                        return request.ReadBodyAsync(buffer, 0, buffer.Length)
                            .Subscribe(
                                n => bytesRead = n,
                                e => o.OnError(e),
                                () =>
                                {
                                    o.OnNext(new ArraySegment<byte>(buffer, 0, bytesRead));
                                    o.OnCompleted();
                                });
                    });
                }
                while (bytesRead > 0);
            }

            [Path("/yield")]
            public IEnumerable<object> Yield()
            {
                object phue = null;
                yield return GetPhue().Do(p => phue = p);
                yield return phue;
            }

            public IObservable<object> GetPhue()
            {
                return new object[] { new { bar = "phue" } }.ToObservable();
            }
        }
    }
}
