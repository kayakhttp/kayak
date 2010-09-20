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
        //public static IDisposable UseFramework(this IObservable<ISocket> sockets)
        //{
        //    return sockets.ToContexts().UseFramework(Assembly.GetCallingAssembly().GetTypes());
        //}

        //public static IDisposable UseFramework(this IObservable<IKayakContext> contexts)
        //{
        //    return contexts.UseFramework(Assembly.GetCallingAssembly().GetTypes());
        //}

        //public static IDisposable UseFramework(this IObservable<ISocket> sockets, Type[] types)
        //{
        //    return sockets.ToContexts().UseFramework(types);
        //}

        //public static IDisposable UseFramework(this IObservable<IKayakContext> contexts, Type[] types)
        //{
        //    return contexts.UseFramework(KayakInvocationBehavior.CreateDefaultBehavior(types));
        //}

        //public static IDisposable UseFramework(this IObservable<ISocket> sockets, IInvocationBehavior behavior)
        //{
        //    return sockets.ToContexts().UseFramework(behavior);
        //}

        //public static IDisposable UseFramework(this IObservable<IKayakContext> contexts, IInvocationBehavior behavior)
        //{
        //    return contexts.Subscribe(c => c.PerformInvocation(behavior));
        //}

        public static IDisposable UseFramework2(this IObservable<IKayakContext> contexts, Type[] types)
        {
            var mm = types.CreateMethodMap();
            
            //Func<IKayakContext, IObservable<InvocationInfo>, IObservable<InvocationInfo>> bindMethod = null;
            //Func<IKayakContext, IObservable<InvocationInfo>, IObservable<InvocationInfo>> bindTarget = null;
            //Func<IKayakContext, IObservable<InvocationInfo>, IObservable<InvocationInfo>> bindHeaderArgs = null;
            //Func<IKayakContext, IObservable<InvocationInfo>, IObservable<InvocationInfo>> bindJsonArgs = null;

            //Action<IKayakContext, IObservable<object>> serializeJson = null;

            //return contexts.UseFramework(c =>
            //    {
            //        return bindJsonArgs(c, bindHeaderArgs(c, bindTarget(c, bindMethod(c, CreateInvocationInfo()))));
            //    },
            //    (c, i) =>
            //    {
            //        serializeJson(c, i);
            //    });

            TypedJsonMapper jsonMapper = new TypedJsonMapper();
            jsonMapper.AddDefaultInputConversions();
            jsonMapper.AddDefaultOutputConversions();

            return contexts.UseFramework(c => 
                InvocationInfo.CreateObservable()
                .BindMethodAndTarget(c, mm)
                .BindHeaderArgs(c)
                .BindJsonArgs(c, jsonMapper), (c, i) => i.SerializeToJson(c, jsonMapper).End(c));
        }

        public static IDisposable UseFramework(this IObservable<IKayakContext> contexts,
            Func<IKayakContext, IObservable<InvocationInfo>> bind,
            Action<IKayakContext, IObservable<object>> ret)
        {
            return contexts.Subscribe(c => ret(c, c.AsInvocation(bind(c))));
        }
    }

    public static partial class Extensions
    {
        public static void End<T>(this IObservable<T> o, IKayakContext context)
        {
            EndInternal(o, context).AsCoroutine<T>().Subscribe(t => 
                {
                    // do something with object resulting from context?
                }, e =>
                {
                    Console.WriteLine("Error during context.");
                    Console.Out.WriteException(e);
                },
                () =>
                {
                    //Console.WriteLine("[{0}] {1} {2} {3} : {4} {5} {6}", DateTime.Now,
                    //    context.Request.Verb, context.Request.Path, context.Request.HttpVersion,
                    //    context.Response.HttpVersion, context.Response.StatusCode, context.Response.ReasonPhrase);
                });
        }

        static IEnumerable<object> EndInternal<T>(this IObservable<T> observable, IKayakContext context)
        {
            yield return observable;
            yield return context.Response.End();
        }
    }
}
