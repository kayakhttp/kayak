using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;

namespace Kayak.Framework
{
    public static partial class Extensions
    {
        public static void BindNamedParameters(this InvocationInfo info, IDictionary<string, string> dict, Func<string, Type, object> coerce)
        {
            var parameters = info.Method.GetParameters().Where(p => !RequestBodyAttribute.IsDefinedOn(p));

            foreach (ParameterInfo param in parameters)
            {
                string value = null;

                if (value != null)
                    try
                    {
                        info.Arguments[param.Position] = coerce(value, param.ParameterType);
                    }
                    catch (Exception e)
                    {
                        throw new Exception(string.Format(
                            "Could not convert '{0}' to the type required by {1} ({2}).",
                            value, param.Name, param.ParameterType), e);
                    }
            }
        }

    }
}
