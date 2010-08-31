using System;
using System.Linq;
using System.Reflection;

namespace Kayak.Framework
{
    public class HeaderBinder : IInvocationArgumentBinder
    {
        Func<string, Type, object> coerce;

        public HeaderBinder(Func<string, Type, object> coerce)
        {
            this.coerce = coerce;
        }

        public void BindArgumentsFromHeaders(IKayakContext context, InvocationInfo info)
        {
            var request = context.Request;
            var parameters = info.Method.GetParameters().Where(p => !RequestBodyAttribute.IsDefinedOn(p));
            var pathParams = context.Items[MethodMap.PathParamsContextKey] as NameValueDictionary;

            foreach (ParameterInfo param in parameters)
            {
                string value = null;

                if (pathParams != null)
                    value = pathParams[param.Name];

                if (value == null)
                    value = request.QueryString[param.Name];

                // TODO also pull from cookies?

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

        public IObservable<Unit> BindArgumentsFromBody(IKayakContext context, InvocationInfo info)
        {
            return null;
        }
    }
}
