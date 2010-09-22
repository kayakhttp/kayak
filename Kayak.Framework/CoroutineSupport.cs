using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Kayak.Framework
{
    public static partial class Extensions
    {
        public static IObservable<Unit> InvokeCoroutine(this IKayakContext context)
        {
            var info = context.GetInvocationInfo();

            if (!(info.Result is IEnumerable<object>))
                return null;

            var continuation = info.Result as IEnumerable<object>;

            return continuation.AsCoroutine<Unit>();
        }
    }
}
