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

    public static partial class Extensions
    {
        public static IDisposable UseFramework(this IObservable<ISocket> sockets)
        {
            TypedJsonMapper jsonMapper = new TypedJsonMapper();
            jsonMapper.AddDefaultInputConversions();
            jsonMapper.AddDefaultOutputConversions();

            return sockets.UseFramework(new KayakFrameworkBehavior(Assembly.GetCallingAssembly().GetTypes()));
        }

        public static IDisposable UseFramework(this IObservable<ISocket> sockets, IKayakFrameworkBehavior behavior)
        {
            return sockets.Subscribe(SocketObserver(behavior));
        }

        static IObserver<ISocket> SocketObserver(IKayakFrameworkBehavior behavior)
        {
            return Observer.Create<ISocket>(s =>
                {
                    var c = KayakContext.CreateContext(s);
                    var process = c.ProcessContext(behavior)
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

        static IObservable<Unit> ProcessContext(this IKayakContext context, IKayakFrameworkBehavior behavior)
        {
            return context.ProcessContextInternal(behavior).AsCoroutine<Unit>();
        }

        static IEnumerable<object> ProcessContextInternal(this IKayakContext context, IKayakFrameworkBehavior behavior)
        {
            yield return context.Request.Begin();

            var info = new InvocationInfo();
            context.SetInvocationInfo(info);

            yield return behavior.Route(context);

            var failed = false;
            var auth = behavior.Authenticate(context);

            if (auth != null)
                yield return auth.Do(r => failed = r);

            if (!failed)
            {
                info.Target = Activator.CreateInstance(info.Method.DeclaringType);

                var parameterCount = info.Method.GetParameters().Length;
                info.Arguments = new object[parameterCount];

                yield return behavior.Bind(context);

                context.PerformInvocation();

                yield return behavior.Handle(context);
            }

            yield return context.Response.End();
        }
    }
}
