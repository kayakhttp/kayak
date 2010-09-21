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

        public static IObservable<IKayakContext> UseFramework(this IObservable<IKayakContext> contexts, Type[] types)
        {
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


            //return contexts.Subscribe(c =>
            //    c.Request.Begin()
            //    .BindMethodAndTarget(c, methodMap)
            //    .BindHeaderArgs(c)
            //    .BindJsonArgs(c, jsonMapper)
            //    .PerformInvocation(c)
            //    .SerializeToJson(c, jsonMapper)
            //    .End(c));

            var methodMap = types.CreateMethodMap();

            TypedJsonMapper jsonMapper = new TypedJsonMapper();
            jsonMapper.AddDefaultInputConversions();
            jsonMapper.AddDefaultOutputConversions();

            return contexts
                .BeginRequest()
                .SelectMethodAndTarget(methodMap)
                .DeserializeArgsFromHeaders()
                .DeserializeArgsFromJson(jsonMapper)
                .PerformInvocation()
                .ServeFile()
                .SerializeToJson(jsonMapper)
                .EndResponse();
        }
    }

    public static partial class Extensions
    {
        public static IObservable<IKayakContext> BeginRequest(this IObservable<IKayakContext> contexts)
        {
            return contexts.SelectMany(c => Observable.CreateWithDisposable<IKayakContext>(o =>
                    c.Request.Begin().Subscribe(u => {}, e => o.OnError(e), () => { o.OnNext(c); o.OnCompleted(); })
                ));
        }

        public static IObservable<IKayakContext> EndResponse(this IObservable<IKayakContext> contexts)
        {
            return contexts.SelectMany(c => Observable.CreateWithDisposable<IKayakContext>(o =>
                    c.Response.End().Subscribe(u => { }, e => o.OnError(e), () => { o.OnNext(c); o.OnCompleted(); })
                ));
        }
    }
}
