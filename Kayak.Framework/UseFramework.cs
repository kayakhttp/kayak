using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using System.Disposables;
using LitJson;

namespace Kayak.Framework
{
    public interface IKayakFrameworkBehavior
    {
        IObservable<Unit> Route(IKayakContext context);
        IObservable<bool> Authenticate(IKayakContext context);
        IObservable<Unit> Bind(IKayakContext context);
        IObservable<Unit> Handle(IKayakContext context);
    }

    public class KayakFrameworkBehavior : IKayakFrameworkBehavior
    {
        public Func<IKayakContext, MethodInfo> RouteFunc { get; set; }
        public TypedJsonMapper JsonMapper { get; set; }

        public KayakFrameworkBehavior(Type[] types)
        {
            var methodMap = types.CreateMethodMap();
            RouteFunc = c => methodMap.GetMethodForContext(c);
            JsonMapper = new TypedJsonMapper();
            JsonMapper.AddDefaultInputConversions();
            JsonMapper.AddDefaultOutputConversions();
        }

        public virtual IObservable<Unit> Route(IKayakContext context)
        {
            context.GetInvocationInfo().Method = RouteFunc(context);
            return null;
        }

        public virtual IObservable<bool> Authenticate(IKayakContext context)
        {
            return null;
        }

        public virtual IObservable<Unit> Bind(IKayakContext context)
        {
            context.DeserializeArgsFromHeaders();
            return context.DeserializeArgsFromJson(JsonMapper);
        }

        public virtual IObservable<Unit> Handle(IKayakContext context)
        {
            return context.HandleWithCoroutine(HandleInvocation) ?? HandleInvocation(context);
        }

        public virtual IObservable<Unit> HandleInvocation(IKayakContext context)
        {
            if (context.GetInvocationInfo().Result == null) return null;

            return context.ServeFile() ?? context.SerializeResultToJson(JsonMapper);
        }
    }

    public static partial class Extensions
    {
        public static IDisposable UseFramework(this IObservable<ISocket> sockets)
        {
            TypedJsonMapper jsonMapper = new TypedJsonMapper();
            jsonMapper.AddDefaultInputConversions();
            jsonMapper.AddDefaultOutputConversions();

            return sockets.UseFramework(Assembly.GetCallingAssembly().GetTypes());
        }
        public static IDisposable UseFramework(this IObservable<ISocket> sockets, Type[] types)
        {
            var mm = types.CreateMethodMap();

            return sockets.Subscribe(SocketObserver(new KayakFrameworkBehavior(types)));
        }

        static IObserver<ISocket> SocketObserver(IKayakFrameworkBehavior behavior)
        {
            return Observer.Create<ISocket>(s =>
                {
                    var c = KayakContext.CreateContext(s);
                    var process = c.ProcessContext2(behavior)
                        .Subscribe(cx =>
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

        static IEnumerable<object> ProcessContextInternal(this IKayakContext context, MethodMap methodMap, TypedJsonMapper jsonMapper)
        {
            yield return context.Request.Begin();

            var info = new InvocationInfo();
            context.SetInvocationInfo(info);

            var method = methodMap.GetMethodForContext(context);
            info.Method = method;
            info.Target = Activator.CreateInstance(method.DeclaringType);

            var parameterCount = info.Method.GetParameters().Length;
            info.Arguments = new object[parameterCount];


            context.DeserializeArgsFromHeaders();

            yield return context.DeserializeArgsFromJson(jsonMapper);

            context.PerformInvocation();

            var response = context.ServeFile() ?? context.SerializeResultToJson(jsonMapper);

            if (response != null)
                yield return response;

            yield return context.Response.End();
        }

        static IObservable<Unit> ProcessContext2(this IKayakContext context, IKayakFrameworkBehavior behavior)
        {
            return context.ProcessContextInternal2(behavior).AsCoroutine<Unit>();
        }

        static IEnumerable<object> ProcessContextInternal2(this IKayakContext context, IKayakFrameworkBehavior behavior)
        {
            yield return context.Request.Begin();

            var info = new InvocationInfo();
            context.SetInvocationInfo(info);

            yield return behavior.Route(context);

            info.Target = Activator.CreateInstance(info.Method.DeclaringType);

            var parameterCount = info.Method.GetParameters().Length;
            info.Arguments = new object[parameterCount];

            var failed = false;
            var auth = behavior.Authenticate(context);

            if (auth != null)
                yield return auth.Do(r => failed = r);

            if (!failed)
            {
                yield return behavior.Bind(context);

                context.PerformInvocation();

                yield return behavior.Handle(context);
            }


            yield return context.Response.End();
        }
    }
}
