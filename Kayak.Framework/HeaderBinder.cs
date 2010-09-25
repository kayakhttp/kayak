using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;

namespace Kayak.Framework
{
    public static partial class Extensions
    {
        public static void DeserializeArgsFromHeaders(this IKayakContext context)
        {
            var request = context.Request;
            var i = context.GetInvocationInfo();
            var parameters = i.Method.GetParameters().Where(p => !RequestBodyAttribute.IsDefinedOn(p));

            var pathParams = context.Items.ContainsKey(MethodMap.PathParamsContextKey) ?
                context.Items[MethodMap.PathParamsContextKey] as Dictionary<string, string> : null;

            foreach (ParameterInfo param in parameters)
            {
                string value = null;

                if (pathParams != null && pathParams.ContainsKey(param.Name))
                    value = pathParams[param.Name];

                var qs = request.GetQueryString();

                if (value == null && qs.ContainsKey(param.Name))
                    value = qs[param.Name];

                // TODO also pull from cookies?

                if (value != null)
                    try
                    {
                        i.Arguments[param.Position] = context.Coerce(value, param.ParameterType);
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
