using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using LitJson;
using System.Text.RegularExpressions;

namespace Kayak.Framework
{
    public static partial class Extensions
    {
        public static IDisposable UseFramework(this IObservable<ISocket> connections)
        {
            return connections.UseFramework(Assembly.GetCallingAssembly().GetTypes());
        }

        public static IDisposable UseFramework(this IObservable<ISocket> connections, Type[] types)
        {
            return connections.UseFramework(InvocationBehavior.CreateDefaultBehavior(types));
        }

        public static IDisposable UseFramework(this IObservable<ISocket> connections, IInvocationBehavior behavior)
        {
            return connections
                .Subscribe(connection =>
                    {
                        var context = new KayakContext(connection);

                        context.Subscribe(u =>
                        {
                            // this callback happens after headers are read.

                            // somewhat analogous to UseFramework()...
                            context.PerformInvocation(behavior);
                        },
                        e =>
                        {
                            // error from context (errors from invoked methods are not handled here,
                            // only errors from IInvocationBehavior, etc)
                            Console.WriteLine("Exception while processing context!");
                            Console.Out.WriteException(e);
                        },
                        () =>
                        {
                            // context completed
                            Console.WriteLine("[{0}] {1} {2} {3} -> {4} {5} {6}", DateTime.Now,
                                context.Request.Verb, context.Request.Path, context.Request.HttpVersion,
                                context.Response.HttpVersion, context.Response.StatusCode, context.Response.ReasonPhrase);
                        });
                    }, 
                    e => 
                    { 
                        // error from listener or while creating context
                    }, 
                    () => 
                    { 
                        // listener completed
                    });
        }

        // this should return IDisposable but i'm not sure what to do with it
        public static void PerformInvocation(this IKayakContext context, IInvocationBehavior behavior)
        {
            PerformInvocationInternal(context, behavior).AsCoroutine<Unit>()
                .Subscribe(o => { }, e =>
                    {
                        // exception thrown by invoked methods don't come here, this is 
                        // only if something went wrong in PerformInvocationInternal
                        // with the IInvocationBehavior, etc

                        // pass it on to the context
                        context.OnError(e);
                    },
                    () =>
                    {
                        // nothing to do! 
                        // behavior's handler should call context.OnCompleted()
                    });
        }

        static IEnumerable<object> PerformInvocationInternal(IKayakContext context, IInvocationBehavior behavior)
        {
            InvocationInfo info = null;

            yield return behavior.GetBinder(context).Do(i => info = i);

            if (info.Method == null || info.Target == null)
            {
                Console.WriteLine("Method or target was null.");
                yield break;
            }

            object result = null;
            bool error = false;

            IObserver<object> handler = behavior.GetHandler(context, info);

            try
            {
                result = info.Invoke();
            }
            catch (Exception e)
            {
                error = true;
                handler.OnError(e);
            }

            if (!error)
            {
                if (info.Method.ReturnType != typeof(void))
                    handler.OnNext(result);

                handler.OnCompleted();
            }
        }
    }
}
