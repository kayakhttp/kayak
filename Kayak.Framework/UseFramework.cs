using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using System.Disposables;

namespace Kayak.Framework
{
    public static partial class Extensions
    {
        public static IDisposable UseFramework(this IObservable<ISocket> sockets)
        {
            return sockets.ToContexts().UseFramework(Assembly.GetCallingAssembly().GetTypes());
        }

        public static IDisposable UseFramework(this IObservable<IKayakContext> contexts)
        {
            return contexts.UseFramework(Assembly.GetCallingAssembly().GetTypes());
        }

        public static IDisposable UseFramework(this IObservable<ISocket> sockets, Type[] types)
        {
            return sockets.ToContexts().UseFramework(types);
        }

        public static IDisposable UseFramework(this IObservable<IKayakContext> contexts, Type[] types)
        {
            return contexts.UseFramework(KayakInvocationBehavior.CreateDefaultBehavior(types));
        }

        public static IDisposable UseFramework(this IObservable<ISocket> sockets, IInvocationBehavior behavior)
        {
            return sockets.ToContexts().UseFramework(behavior);
        }

        public static IDisposable UseFramework(this IObservable<IKayakContext> contexts, IInvocationBehavior behavior)
        {
            return contexts.Subscribe(c => c.PerformInvocation(behavior));
        }
    }
}
