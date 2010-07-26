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
}
