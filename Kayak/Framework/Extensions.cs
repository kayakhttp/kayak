using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using LitJson;
using System.Text.RegularExpressions;

namespace Kayak.Framework
{
    public interface IInvocationBehavior
    {
        IObservable<InvocationInfo> GetBinder(IKayakContext context);
        IObserver<object> GetHandler(IKayakContext context, InvocationInfo info);
    }

    public static partial class Extensions
    {
        public static IDisposable UseFramework(this IObservable<ISocket> connections)
        {
            return connections.UseFramework(Assembly.GetCallingAssembly().GetTypes());
        }

        public static IDisposable UseFramework(this IObservable<ISocket> connections, Type[] types)
        {
            var behavior = new KayakInvocationBehavior();
            behavior.MapTypes(types);
            behavior.AddJsonSupport();

            return connections.UseFramework(behavior);
        }

        public static IDisposable UseFramework(this IObservable<ISocket> connections, IInvocationBehavior behavior)
        {
            return connections.UseFramework(connection => new KayakContext(connection), behavior);
        }

        public static IDisposable UseFramework(this IObservable<ISocket> connections, Func<ISocket, IKayakContext> contextFactory, IInvocationBehavior behavior)
        {
            return connections
                .Subscribe(connection =>
                    {
                        var context = contextFactory(connection);

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

        public static void PerformInvocation(this IKayakContext context, IInvocationBehavior behavior)
        {
            if (behavior == null)
                throw new ArgumentNullException("behavior");

            PerformInvocationInternal(context, behavior).AsCoroutine<Unit>()
                .Subscribe(o => { }, e =>
                    {
                        // exceptions thrown by invoked methods don't come here, this is 
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

            var binder = behavior.GetBinder(context);

            if (binder == null)
                throw new Exception("Behavior returned null binder.");

            yield return binder.Do(i => info = i);

            if (info == null)
                throw new Exception("Binder returned by behavior did not yield an instance of InvocationInfo.");
            if (info.Method == null)
                throw new Exception("Binder returned by behavior did not yield an valid instance of InvocationInfo. Method was null.");
            if (info.Target == null)
                throw new Exception("Binder returned by behavior did not yield an valid instance of InvocationInfo. Target was null.");

            IObserver<object> handler = behavior.GetHandler(context, info);

            if (handler == null)
                throw new Exception("Behavior returned null handler.");

            object result = null;
            bool error = false;

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
