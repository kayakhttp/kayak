using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Kayak.Core;
using LitJson;

namespace Kayak.Framework
{
    public class KayakFrameworkResponder2 : IHttpResponder
    {
        MethodMap methodMap;
        TypedJsonMapper mapper;

        public KayakFrameworkResponder2(MethodMap methodMap, TypedJsonMapper mapper)
        {
            this.methodMap = methodMap;
            this.mapper = mapper;
        }

        public object Respond(IHttpServerRequest request)
        {
            return RespondInternal(request).AsCoroutine<IHttpServerResponse>();
        }

        public IEnumerable<object> RespondInternal(IHttpServerRequest request)
        {
            var info = new InvocationInfo();

            var context = request.Context;

            bool notFound, invalidMethod;
            info.Method = methodMap.GetMethod(request.GetPath(), request.Verb, context, out notFound, out invalidMethod);

            if (notFound)
            {
                yield return DefaultResponses.NotFoundResponse();
                yield break;
            }

            if (invalidMethod)
            {
                yield return DefaultResponses.InvalidMethodResponse(request.Verb);
                yield break;
            }

            info.Target = Activator.CreateInstance(info.Method.DeclaringType);
            info.Arguments = new object[info.Method.GetParameters().Length];

            context.SetInvocationInfo(info);

            IDictionary<string, string> target = new Dictionary<string, string>();

            var pathParams = context.GetPathParameters();
            var queryString = request.GetQueryString();

            target.Concat(pathParams, queryString);

            info.BindNamedParameters(target, context.Coerce);

            yield return info.DeserializeArgsFromJson(request, mapper);

            var service = info.Target as KayakService2;

            if (service != null)
            {
                service.Context = context;
                service.Request = request;
            }

            info.Invoke();

            if (info.Result is IHttpServerResponse)
                yield return info.Result;
            else if (info.Result is object[])
            {
                yield return (info.Result as object[]).ToResponse();
            }
            else if (info.Method.ReturnType == typeof(IEnumerable<object>))
            {
                IHttpServerResponse response = null;

                var continuation = info.Result as IEnumerable<object>;
                info.Result = null;

                yield return HandleCoroutine(continuation, info, request, context).Do(r => response = r);
                yield return response;

            }
            else
                yield return GetResponse(request);
        }

        IObservable<IHttpServerResponse> HandleCoroutine(IEnumerable<object> continuation, InvocationInfo info, IHttpServerRequest request, IDictionary<object, object> context)
        {
            return Observable.CreateWithDisposable<IHttpServerResponse>(o => continuation.AsCoroutine<object>().Subscribe(
                      r => info.Result = r,
                      e =>
                      {
                          o.OnNext(GetResponse(request));
                      },
                      () =>
                      {
                          o.OnNext(GetResponse(request));
                          o.OnCompleted();
                      }));
        }

        public virtual IHttpServerResponse GetResponse(IHttpServerRequest request)
        {
            var context = request.Context;
            var info = context.GetInvocationInfo();
            bool minified = context.GetJsonOutputMinified();

            return info.ServeFile() ?? info.GetJsonResponse(mapper, minified);
        }
    }
}
