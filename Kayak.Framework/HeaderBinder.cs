using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;

namespace Kayak.Framework
{
    public static partial class Extensions
    {
        public static IObservable<InvocationInfo> BindHeaderArgs(this IObservable<InvocationInfo> bind, IKayakContext c)
        {
            return bind.Select(i =>
            {
                var request = c.Request;
                var parameters = i.Method.GetParameters().Where(p => !RequestBodyAttribute.IsDefinedOn(p));

                var pathParams = c.Items.ContainsKey(MethodMap.PathParamsContextKey) ?
                    c.Items[MethodMap.PathParamsContextKey] as Dictionary<string, string> : null;

                foreach (ParameterInfo param in parameters)
                {
                    string value = null;

                    if (pathParams != null && pathParams.ContainsKey(param.Name))
                        value = pathParams[param.Name];

                    if (value == null && request.QueryString.ContainsKey(param.Name))
                        value = request.QueryString[param.Name];

                    // TODO also pull from cookies?

                    if (value != null)
                        try
                        {
                            i.Arguments[param.Position] = c.Coerce(value, param.ParameterType);
                        }
                        catch (Exception e)
                        {
                            throw new Exception(string.Format(
                                "Could not convert '{0}' to the type required by {1} ({2}).",
                                value, param.Name, param.ParameterType), e);
                        }
                }

                return i;
            });
        }

    }
}
