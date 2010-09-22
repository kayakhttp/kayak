using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using System.Disposables;
using LitJson;

namespace Kayak.Framework
{
    public static partial class Extensions
    {
        public static IDisposable UseFramework(this IObservable<ISocket> sockets, Type[] types)
        {
            var methodMap = types.CreateMethodMap();

            TypedJsonMapper jsonMapper = new TypedJsonMapper();
            jsonMapper.AddDefaultInputConversions();
            jsonMapper.AddDefaultOutputConversions();

            return sockets.Subscribe(s =>
                {
                    var c = KayakContext.CreateContext(s);
                    c.ProcessContext(methodMap, jsonMapper).Subscribe(cx =>
                        {
                        },
                        e =>
                        {
                            Console.WriteLine("Error during context.");
                            Console.Out.WriteException(e);
                        },
                        () =>
                        {
                            Console.WriteLine("[{0}] {1} {2} {3} : {4} {5} {6}", DateTime.Now,
                            c.Request.Verb, c.Request.Path, c.Request.HttpVersion,
                            c.Response.HttpVersion, c.Response.StatusCode, c.Response.ReasonPhrase);
                        });
                },
                e =>
                {
                    Console.Out.WriteLine("Error from socket source.");
                    Console.Out.WriteException(e);
                },
                () => { Console.Out.WriteLine("Socket source completed."); });
        }

        static IObservable<Unit> ProcessContext(this IKayakContext context, MethodMap methodMap, TypedJsonMapper jsonMapper)
        {
            return ProcessContextInternal(context, methodMap, jsonMapper).AsCoroutine<Unit>();
        }

        static IEnumerable<object> ProcessContextInternal(this IKayakContext context, MethodMap methodmap, TypedJsonMapper jsonMapper)
        {
            yield return context.Request.Begin();

            context.SelectMethodAndTarget(methodmap);
            context.DeserializeArgsFromHeaders();

            yield return context.DeserializeArgsFromJson(jsonMapper);

            context.PerformInvocation();

            var response = context.ServeFile() ?? context.SerializeResultToJson(jsonMapper);

            if (response != null)
                yield return response;

            yield return context.EndResponse();
        }

        public static IObservable<IKayakContext> BeginRequest(this IKayakContext context)
        {
            return Observable.CreateWithDisposable<IKayakContext>(o =>
                    context.Request.Begin().Subscribe(u => { }, e => o.OnError(e), () => { o.OnNext(context); o.OnCompleted(); })
                );
        }

        public static IObservable<IKayakContext> EndResponse(this IKayakContext context)
        {
            return Observable.CreateWithDisposable<IKayakContext>(o =>
                    context.Response.End().Subscribe(u => { }, e => o.OnError(e), () => { o.OnNext(context); o.OnCompleted(); })
                );
        }
    }
}
