using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Kayak;
using Kayak.Framework;
using Owin;
using System.Threading.Tasks;

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
            [Path("/hello")]
            public object SayHello(string name)
            {
                // name is from the query string.
                return "hello, " + name + ".";
            }

            // you can accept request data deserialized from JSON
            // into strongly-typed objects. make sure your class 
            // and its fields/properties are public...
            public class Foo
            {
                public string Name;
                public string Color;
            }

            static Foo foo;

            // ...decorate with the appropriate verbs.
            [Verb("PUT")]
            [Path("/foo")]
            public IResponse PutFoo([RequestBody] Foo f)
            {
                foo = f; // a Foo from JSON!
                return new KayakResponse("200 OK", new { message = "foo was updated." });
            }

            // GET is implied.
            [Path("/foo")]
            public object GetFoo()
            {
                return foo;
            }

            // You can serve files too!
            [Path("/files/{name}")]
            public FileInfo GetFile(string name)
            {
                return new FileInfo(name);
            }

            // Coroutines without the Async CTP!
            [Path("/yield")]
            public IEnumerable<object> Yield()
            {
                var fetch = FetchFromDatabase();
                // yield a Task to await its completion
                yield return fetch;

                // always check for Task exceptions, they're
                // not thrown automatically.
                if (fetch.Exception != null) 
                    // string the aggregate exception
                    throw fetch.Exception.InnerException;

                // yield anything other than a task, and it will be
                // treated as just if it had been returned from
                // a regular method
                yield return fetch.Result;
            }

            public Task<Foo> FetchFromDatabase()
            {
                // ...
                return null;
            }

            [Verb("POST")]
            [Path("/postvars")]
            public object PostVars()
            {
                // TODO parse application/x-www-form-urlencoded
                return null;
            }

            // exceptions han
            [Path("/error")]
            public object Error()
            {
                throw new Exception("Uh oh.");
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
        }
    }
}
