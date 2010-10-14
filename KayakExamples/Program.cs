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
                    200, 
                    new Dictionary<string, string>()
                    {
                        { "Content-Type", "text/plain" }
                    }, 
                    //"Hello world." 
                    MyService2.EchoGenerator(request)
                };
                /*
                return Observable.Create<IHttpServerResponse>(o =>
                {
                    o.OnNext(new BufferedResponse("asdf"));
                    o.OnCompleted();
                    return () => { };
                });
                */
                //return new IHttpServerResponse[] { new BufferedResponse("Simple hello.") }.ToObservable();
            }

            #endregion
        }



        static void New()
        {
            var server = new KayakServer();
            
            var mm = new Type[] { typeof(MyService2) }.CreateMethodMap();
            var jm = new TypedJsonMapper();
            jm.AddDefaultInputConversions();
            jm.AddDefaultOutputConversions();

            server.RespondWith(new KayakFrameworkResponder2(mm, jm));
        }

    }

    public class MyService2 : KayakService2
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

    //public class MyService : KayakService
    //{
    //    [Path("/")]
    //    public IEnumerable<object> Root()
    //    {
    //        yield return Response.Write("Hello.");
    //    }

    //    [Path("/hello")]
    //    public object SayHello(string name)
    //    {
    //        return new { greeting = "hello, " + name + ".", number = 5 };
    //    }

    //    static object foo;

    //    [Verb("POST")]
    //    [Verb("PUT")]
    //    [Path("/foo")]
    //    public void PutFoo([RequestBody] object t)
    //    {
    //        foo = t;
    //    }

    //    [Path("/foo")]
    //    public object GetFoo()
    //    {
    //        return foo;
    //    }

    //    [Path("/error")]
    //    public void Error()
    //    {
    //        throw new Exception("Uh oh.");
    //    }

    //    [Path("/path/{p}")]
    //    public IEnumerable<object> PathParam(string p)
    //    {
    //        yield return Response.Write(new ArraySegment<byte>(Encoding.UTF8.GetBytes("The value of p is '" + p + "'.")));
    //    }

    //    [Path("/files/{name}")]
    //    public FileInfo GetFile(string name)
    //    {
    //        return new FileInfo(name);
    //    }

    //    [Verb("POST")]
    //    [Verb("PUT")]
    //    [Path("/echo")]
    //    public IEnumerable<object> Echo()
    //    {
    //        int contentLength = -1;
            
    //        if (Request.Headers.ContainsKey("Content-Length"))
    //            contentLength = int.Parse(Request.Headers["Content-Length"]);

    //        if (contentLength != -1)
    //            Response.Headers["Content-Length"] = contentLength.ToString();

    //        int bytesRead = 0;

    //        while (bytesRead < contentLength)
    //        {
    //            var data = default(ArraySegment<byte>);
    //            yield return Request.Read().Do(d => data = d);
    //            yield return Response.Write(data);
    //            bytesRead += data.Count;
    //        }
    //    }

    //    [Path("/yield")]
    //    public IEnumerable<object> Yield()
    //    {
    //        object phue = null;
    //        yield return GetPhue().Do(p => phue = p);
    //        yield return phue;
    //    }

    //    public IObservable<object> GetPhue()
    //    {
    //        return new object[] { new { bar = "phue" } }.ToObservable();
    //    }
    //}
}
