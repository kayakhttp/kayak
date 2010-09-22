using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;

namespace Kayak.Framework
{
    /// <summary>
    /// Defines a method invocation, including the target object, the `MethodInfo` to be invoked, and the arguments to the method.
    /// </summary>
    public sealed class InvocationInfo
    {
        public object Target;
        public MethodInfo Method;
        public object[] Arguments;
        public object Result;
        public Exception Exception;

        public void Invoke()
        {
            if (Method == null)
                throw new Exception("Context has invalid instance of InvocationInfo. Method was null.");
            if (Target == null)
                throw new Exception("Context has invalid instance of InvocationInfo. Target was null.");

            try
            {
                Result = Method.Invoke(Target, Arguments);
            }
            catch (Exception e)
            {
                Exception = e;
            }
        }

        public override string ToString()
        {
            if (Method == null) return base.ToString();
            return Method.DeclaringType.Namespace + "." + Method.DeclaringType.Name + "." + Method.Name;
        }
    }

    public static partial class Extensions
    {
        static object InvocationInfoContextKey = new object();

        public static InvocationInfo GetInvocationInfo(this IKayakContext context)
        {
            if (!context.Items.ContainsKey(InvocationInfoContextKey)) return null;
            return context.Items[InvocationInfoContextKey] as InvocationInfo;
        }

        internal static void SetInvocationInfo(this IKayakContext context, InvocationInfo info)
        {
            context.Items[InvocationInfoContextKey] = info;
        }

        public static void PerformInvocation(this IKayakContext context)
        {
            var info = context.GetInvocationInfo();

            if (info == null)
                throw new Exception("Context has no InvocationInfo.");

            var service = info.Target as KayakService;

            if (service != null)
                service.Context = context;

            info.Invoke();
        }
    }
}
