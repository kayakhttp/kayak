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
            foreach (var dict in srcs.Where(s => s != null))
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
}
