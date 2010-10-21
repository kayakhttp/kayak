using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Kayak;
using Kayak.Framework;
using System.IO;
using System.Reflection;
using LitJson;
using Kayak.Core;

namespace KayakExamples
{
    class Program
    {
        static void Main(string[] args)
        {
            New();
            //Simple();
        }

        static void Simple()
        {
            IObservable<ISocket> sockets = new KayakServer();
            IHttpResponder responder = new SampleResponder();

            sockets.RespondWith(responder);
        }

        class SampleResponder : IHttpResponder
        {
            #region IHttpResponder Members

            public object Respond(IHttpServerRequest request)
            {
                return new object[] { 
                    "200 OK", 
                    new Dictionary<string, string>()
                    {
                        { "Content-Type", "text/plain" }
                    }, 
                    // "Hello world."
                    ExampleService.EchoGenerator(request)
                };
            }

            #endregion
        }

        static void New()
        {
            var server = new KayakServer();
            
            var mm = new Type[] { typeof(ExampleService) }.CreateMethodMap();
            var jm = new JsonMapper2();
            jm.AddDefaultInputConversions();
            jm.AddDefaultOutputConversions();

            server.RespondWith(new KayakFrameworkResponder(mm, jm));
        }
    }

    public class ExampleService : KayakService
    {
        [Path("/")]
        public object Root()
        {
            var response = new BufferedResponse();
            response.Add("They speak English in What?");
            return response;
        }

        [Path("/hello")]
        public object SayHello(string name)
        {
            return new { greeting = "hello, " + name + "." };
        }

        static object foo;

        [Verb("POST")]
        [Verb("PUT")]
        [Path("/foo")]
        public void PutFoo([RequestBody] object t)
        {
            foo = t;
        }

        [Path("/foo")]
        public object GetFoo()
        {
            return foo;
        }

        [Path("/error")]
        public void Error()
        {
            throw new Exception("Uh oh.");
        }

        [Path("/files/{name}")]
        public FileInfo GetFile(string name)
        {
            return new FileInfo(name);
        }

        [Verb("POST")]
        [Verb("PUT")]
        [Path("/echo")]
        public object Echo()
        {
            int contentLength = -1;

            if (Request.Headers.ContainsKey("Content-Length"))
                contentLength = int.Parse(Request.Headers["Content-Length"]);

            return new object[] {
                "200 OK",
                new Dictionary<string, string>()
                {
                    { "Content-Length", contentLength.ToString() }
                },
                EchoGenerator(Request)
            };
        }

        public static IEnumerable<object> EchoGenerator(IHttpServerRequest request)
        {
            var buffer = new byte[2048];

            foreach (var getChunk in request.GetBody())
            {
                var bytesRead = 0;
                yield return Observable.CreateWithDisposable<ArraySegment<byte>>(o =>
                    {
                        return getChunk(new ArraySegment<byte>(buffer, 0, buffer.Length)).Subscribe(n => bytesRead = n, e => o.OnError(e),
                            () =>
                            {
                                o.OnNext(new ArraySegment<byte>(buffer, 0, bytesRead));
                                o.OnCompleted();
                            });
                    });
            }
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
