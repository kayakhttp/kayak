using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;

namespace Kayak.Framework
{
    public sealed class InvocationInfo
    {
        public object Target;
        public MethodInfo Method;
        public object[] Arguments;

        public object Invoke()
        {
            return Method.Invoke(Target, Arguments);
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
    }
}
