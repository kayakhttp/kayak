using System;
using System.Collections.Generic;
using System.Linq;
using LitJson;
using Owin;

namespace Kayak.Framework
{
    public class KayakFramework : IApplication
    {
        MethodMap methodMap;
        JsonMapper2 mapper;

        public KayakFramework(MethodMap methodMap, JsonMapper2 mapper)
        {
            this.methodMap = methodMap;
            this.mapper = mapper;
        }

        public IAsyncResult BeginInvoke(IRequest request, AsyncCallback callback, object state)
        {
            return new ObservableAsyncResult<IResponse>(InvokeInternal(request).AsCoroutine<IResponse>(), callback);
        }

        public IResponse EndInvoke(IAsyncResult result)
        {
            return ((ObservableAsyncResult<IResponse>)result).GetResult();
        }

        public IEnumerable<object> InvokeInternal(IRequest request)
        {
            var info = new InvocationInfo();

            var context = request.Items;

            bool notFound, invalidMethod;
            info.Method = methodMap.GetMethod(request.GetPath(), request.Method, context, out notFound, out invalidMethod);

            if (notFound)
            {
                yield return DefaultResponses.NotFoundResponse();
                yield break;
            }

            if (invalidMethod)
            {
                yield return DefaultResponses.InvalidMethodResponse(request.Method);
                yield break;
            }

            info.Target = Activator.CreateInstance(info.Method.DeclaringType);
            info.Arguments = new object[info.Method.GetParameters().Length];

            context.SetInvocationInfo(info);

            IDictionary<string, string> target = new Dictionary<string, string>();

            var pathParams = context.GetPathParameters();
            var queryString = request.GetQueryString();

            ConcatDicts(target, pathParams, queryString);

            info.BindNamedParameters(target, context.Coerce);

            yield return info.DeserializeArgsFromJson(request, mapper);

            var service = info.Target as KayakService;

            if (service != null)
            {
                //service.Context = context;
                service.Request = request;
            }

            info.Invoke();

            if (info.Result is IResponse)
                yield return info.Result;
            else if (info.Method.ReturnType == typeof(IEnumerable<object>))
            {
                IResponse response = null;

                var continuation = info.Result as IEnumerable<object>;
                info.Result = null;

                yield return HandleCoroutine(continuation, info, request).Do(r => response = r);
                yield return response;

            }
            else if (info.Method.ReturnType != typeof(void))
                yield return GetResponse(request);
            else
                throw new Exception("Executed void method " + info);
        }

        void ConcatDicts<K, V>(IDictionary<K, V> target, params IDictionary<K, V>[] srcs)
        {
            foreach (var dict in srcs.Where(s => s != null))
                foreach (var pair in dict)
                    target[pair.Key] = dict[pair.Key];
        }

        IObservable<IResponse> HandleCoroutine(IEnumerable<object> continuation, InvocationInfo info, IRequest request)
        {
            return Observable.CreateWithDisposable<IResponse>(o => continuation.AsCoroutine<object>().Subscribe(
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

        public virtual IResponse GetResponse(IRequest request)
        {
            var context = request.Items;
            var info = context.GetInvocationInfo();
            bool minified = context.GetJsonOutputMinified();

            return request.ServeFile() ?? info.GetJsonResponse(mapper, minified);
        }
    }
}
