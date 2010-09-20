using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Kayak.Framework
{
    public static partial class Extensions
    {
        /// <summary>
        /// Carries out a method invocation within the given HTTP context using the given behavior. 
        /// 
        /// First, the behavior's `Bind` method is called to retrieve an observable which will provide 
        /// an `InvocationInfo` specifying the method to be invoked, the target object on which the 
        /// method should be invoked, and arguments to the method.
        /// 
        /// Next, the behavior's `Invoke` method is called with the context and an observable which 
        /// yields the result of the invocation upon subscription.
        /// </summary>
        public static IObservable<object> AsInvocation(this IKayakContext context, IObservable<InvocationInfo> bind)
        {
            if (context == null)
                throw new ArgumentNullException("context");

            if (bind == null)
                throw new ArgumentNullException("bind");

            return context.AsInvocationInternal(bind).AsCoroutine<object>();
        }

        static IEnumerable<object> AsInvocationInternal(this IKayakContext context, IObservable<InvocationInfo> bind)
        {
            yield return context.Request.Begin();

            InvocationInfo info = null;

            yield return bind.Do(i => info = i);

            if (info == null)
                throw new Exception("Bind did not yield an instance of InvocationInfo.");
            if (info.Method == null)
                throw new Exception("Bind did not yield an valid instance of InvocationInfo. Method was null.");
            if (info.Target == null)
                throw new Exception("Bind did not yield an valid instance of InvocationInfo. Target was null.");

            context.SetInvocationInfo(info);

            object result = info.Invoke();

            if (info.Method.ReturnType != typeof(void))
                yield return result;
        }
    }
}
