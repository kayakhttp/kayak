using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Kayak.Framework
{
    public static partial class Extensions
    {
        public static IObservable<IKayakContext> PerformInvocation(this IObservable<IKayakContext> contexts)
        {
            return contexts.Do(c => c.PerformInvocation());
        }

        public static void PerformInvocation(this IKayakContext context)
        {
            var info = context.GetInvocationInfo();

            if (info == null)
                throw new Exception("Context has no InvocationInfo.");

            info.Invoke(); 
        }
    }
}
