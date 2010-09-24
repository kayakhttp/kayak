using System;
using System.Reflection;
using LitJson;

namespace Kayak.Framework
{

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
            context.DeserializeArgsFromHeaders();
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
