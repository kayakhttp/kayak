using System;
using System.Collections.Generic;
using System.Linq;
using LitJson;
using Owin;
using System.Threading.Tasks;
using System.IO;

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
            return InvokeInternal(request).AsCoroutine<IResponse>().ContinueWith(t =>
                {
                    callback(t);
                });
        }

        public IResponse EndInvoke(IAsyncResult result)
        {
            var task = ((Task<IResponse>)result);

            if (task.IsFaulted)
                throw task.Exception;

            return task.Result;
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
                var continuation = info.Result as IEnumerable<object>;

                var coroutine = continuation.AsCoroutine<object>();
                yield return coroutine;

                if (coroutine.IsFaulted)
                    info.Exception = coroutine.Exception;
                else
                    info.Result = coroutine.Result;

                yield return GetResponse(request);
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

        public virtual IResponse GetResponse(IRequest request)
        {
            var context = request.Items;
            var info = context.GetInvocationInfo();

            var response = info.Result as IResponse;

            if (response != null)
                return response;

            var file = info.Result as FileInfo;

            if (file != null)
                return request.ServeFile(file);

            return info.GetJsonResponse(mapper, context.GetJsonOutputMinified());
        }
    }
}
