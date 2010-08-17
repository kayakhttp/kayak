using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Kayak.Framework
{
    public static partial class Extensions
    {
        public static IObservable<Unit> PerformInvocation(this IKayakContext context, IInvocationBehavior behavior)
        {
            if (behavior == null)
                throw new ArgumentNullException("behavior");

            return PerformInvocationInternal(context, behavior).AsCoroutine<Unit>();
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
