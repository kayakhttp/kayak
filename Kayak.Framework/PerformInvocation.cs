using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Kayak.Framework
{
    public static partial class Extensions
    {
        public static void PerformInvocation(this IKayakContext context, IInvocationBehavior behavior)
        {
            behavior.Invoke(context, context.AsInvocation(behavior.Bind(context)));
        }

        public static IObservable<object> AsInvocation(this IKayakContext context, IObservable<InvocationInfo> bind)
        {
            if (context == null)
                throw new ArgumentNullException("context");

            if (bind == null)
                throw new ArgumentNullException("bind");

            return AsInvocationInternal(context, bind).AsCoroutine<object>();
        }

        static IEnumerable<object> AsInvocationInternal(IKayakContext context, IObservable<InvocationInfo> bind)
        {
            yield return context.Request.Begin();

            InvocationInfo info = null;

            yield return bind.Do(i => info = i);

            if (info == null)
                throw new Exception("Binder returned by behavior did not yield an instance of InvocationInfo.");
            if (info.Method == null)
                throw new Exception("Binder returned by behavior did not yield an valid instance of InvocationInfo. Method was null.");
            if (info.Target == null)
                throw new Exception("Binder returned by behavior did not yield an valid instance of InvocationInfo. Target was null.");

            context.SetInvocationInfo(info);

            object result = info.Invoke();

            if (info.Method.ReturnType != typeof(void))
                yield return result;
        }
    }
}
