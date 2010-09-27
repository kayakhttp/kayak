using System;
using System.Reflection;
using LitJson;
using System.Collections.Generic;
using System.Linq;
using Kayak.Core;

namespace Kayak.Framework
{
    public static partial class Extensions
    {
        public static void Concat<K, V>(this IDictionary<K, V> target, params IDictionary<K, V>[] srcs)
        {
            foreach (var dict in srcs)
                foreach (var pair in dict)
                    target[pair.Key] = dict[pair.Key];
        }
    }

    public class KayakFrameworkBehavior : IKayakFrameworkBehavior
    {
        public Func<IKayakContext, MethodInfo> RouteFunc { get; set; }
        public Func<IKayakContext, bool> AuthFunc { get; set; }
        public TypedJsonMapper JsonMapper { get; set; }

        public KayakFrameworkBehavior() : this(Assembly.GetCallingAssembly().GetTypes()) { }
        public KayakFrameworkBehavior(Type[] types)
        {
            var methodMap = types.CreateMethodMap();
            RouteFunc = c => methodMap.GetMethodForContext(c);
            JsonMapper = new TypedJsonMapper();
            JsonMapper.AddDefaultInputConversions();
            JsonMapper.AddDefaultOutputConversions();
        }

        public virtual IObservable<Unit> Route(IKayakContext context)
        {
            context.GetInvocationInfo().Method = RouteFunc(context);
            return null;
        }

        public virtual IObservable<bool> Authenticate(IKayakContext context)
        {
            return null;
        }

        public virtual IObservable<Unit> Bind(IKayakContext context)
        {
            IDictionary<string, string> target = new Dictionary<string, string>();

            target.Concat(context.Items.GetPathParameters(), context.Request.GetQueryString());

            context.GetInvocationInfo().BindNamedParameters(target, context.Coerce);
            return context.DeserializeArgsFromJson(JsonMapper);
        }

        public virtual IObservable<Unit> Handle(IKayakContext context)
        {
            return context.HandleWithCoroutine(HandleInvocation) ?? HandleInvocation(context);
        }

        public virtual IObservable<Unit> HandleInvocation(IKayakContext context)
        {
            var info = context.GetInvocationInfo();

            if (info.Exception == null && info.Result == null) return null;

            return context.ServeFile() ?? context.SerializeResultToJson(JsonMapper);
        }
    }

    public class KayakFrameworkBehavior2 : IKayakFrameworkBehavior2
    {
        public TypedJsonMapper JsonMapper { get; set; }
        MethodMap methodMap;

        public KayakFrameworkBehavior2() : this(Assembly.GetCallingAssembly().GetTypes()) { }
        public KayakFrameworkBehavior2(Type[] types)
        {
            methodMap = types.CreateMethodMap();
            JsonMapper = new TypedJsonMapper();
            JsonMapper.AddDefaultInputConversions();
            JsonMapper.AddDefaultOutputConversions();
        }

        public virtual IObservable<IHttpServerResponse> Route(IHttpServerRequest request, IDictionary<object, object> context)
        {
            context.GetInvocationInfo().Method = methodMap.GetMethod(request.GetPath(), request.RequestLine.Verb, context);
            return null;
        }

        public virtual IObservable<IHttpServerResponse> Authenticate(IHttpServerRequest request, IDictionary<object, object> context)
        {
            return null;
        }

        public virtual IObservable<Unit> Bind(IHttpServerRequest request, IDictionary<object, object> context)
        {
            var info = context.GetInvocationInfo();

            IDictionary<string, string> target = new Dictionary<string, string>();

            var pathParams = context.GetPathParameters();
            var queryString = request.GetQueryString();

            target.Concat(pathParams, queryString);

            info.BindNamedParameters(target, context.Coerce);

            return info.DeserializeArgsFromJson(request, JsonMapper);
        }

        public virtual IObservable<IHttpServerResponse> GetResponse(IHttpServerRequest request, IDictionary<object, object> context)
        {
            var info = context.GetInvocationInfo();

            if (info.Exception == null && info.Result == null) return null;

            if (info.Result is IEnumerable<object>)
            {
                var coroutine = (info.Result as IEnumerable<object>).AsCoroutine<object>();
                info.Result = null;
                return Observable.CreateWithDisposable<IHttpServerResponse>(o => coroutine.Subscribe(
                    r => info.Result = r,
                    e =>
                    {
                        var response = GetResponse2(request, context);

                        if (response != null)
                            o.OnNext(response);
                        else
                            o.OnError(e);
                    },
                    () =>
                    {
                        o.OnNext(GetResponse2(request, context));
                        o.OnCompleted();
                    }));
            }
            else return new IHttpServerResponse[] { GetResponse2(request, context) }.ToObservable();
        }

        public virtual IHttpServerResponse GetResponse2(IHttpServerRequest request, IDictionary<object, object> context)
        {
            var info = context.GetInvocationInfo();

            return info.ServeFile() ?? info.GetJsonResponse(context, JsonMapper);
        }
    }

}
