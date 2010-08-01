using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using LitJson;
using System.IO;

namespace Kayak.Framework
{
    public interface IInvocationBehavior
    {
        IObservable<InvocationInfo> GetBinder(IKayakContext context);
        IObserver<object> GetHandler(IKayakContext context, InvocationInfo info);
    }

    public interface IInvocationArgumentBinder
    {
        void BindArgumentsFromHeaders(IKayakContext context, InvocationInfo info);
        IObservable<Unit> BindArgumentsFromBody(IKayakContext context, InvocationInfo info);
    }

    public interface IInvocationResultHandler
    {
        IObservable<Unit> HandleResult(IKayakContext context, InvocationInfo info, object result);
    }

    public interface IInvocationExceptionHandler
    {
        IObservable<Unit> HandleException(IKayakContext context, InvocationInfo info, Exception exception);
    }

    public class InvocationBehavior : IInvocationBehavior
    {
        public static InvocationBehavior CreateDefaultBehavior(Type[] types)
        {
            var map = types.CreateMethodMap();
            var behavior = new InvocationBehavior(c => map.GetMethodForContext(c));

            var mapper = new TypedJsonMapper();
            mapper.AddDefaultOutputConversions();
            mapper.AddDefaultInputConversions();

            behavior.Binders.Add(new HeaderBinder());
            behavior.Binders.Add(new JsonBinder(mapper));
            behavior.ResultHandlers.Add(new JsonHandler(mapper));
            behavior.ExceptionHandlers.Add(new DefaultExceptionHandler());

            return behavior;
        }

        public List<IInvocationArgumentBinder> Binders { get; private set; }
        public List<IInvocationResultHandler> ResultHandlers { get; private set; }
        public List<IInvocationExceptionHandler> ExceptionHandlers { get; private set; }

        static string InvocationResultContextKey = "InvocationResultContextKey";

        Func<IKayakContext, MethodInfo> map;

        internal InvocationBehavior(Func<IKayakContext, MethodInfo> map)
        {
            this.map = map;
            Binders = new List<IInvocationArgumentBinder>();
            ResultHandlers = new List<IInvocationResultHandler>();
            ExceptionHandlers = new List<IInvocationExceptionHandler>();
        }

        public IObservable<InvocationInfo> GetBinder(IKayakContext context)
        {
            return Bind(context).AsCoroutine<InvocationInfo>();
        }

        IEnumerable<object> Bind(IKayakContext context)
        {
            InvocationInfo info = new InvocationInfo();

            info.Method = map(context);
            info.Target = Activator.CreateInstance(info.Method.DeclaringType);

            var service = info.Target as KayakService;

            if (service != null)
                service.Context = context;

            var argumentCount = info.Method.GetParameters().Length;
            info.Arguments = new object[argumentCount];

            if (argumentCount > 0)
                foreach (var binder in Binders)
                {
                    binder.BindArgumentsFromHeaders(context, info);
                    yield return binder.BindArgumentsFromBody(context, info);
                }

            yield return info;
        }

        public IObserver<object> GetHandler(IKayakContext context, InvocationInfo info)
        {
            return Observer.Create<object>(
                r => InvocationResult(context, info, r), 
                e => InvocationException(context, info, e), 
                () => InvocationCompleted(context, info));
        }

        void InvocationCompleted(IKayakContext context, InvocationInfo info)
        {
            if (context.Items.ContainsKey(InvocationResultContextKey))
            {
                object result = context.Items[InvocationResultContextKey];

                foreach (var resultHandler in ResultHandlers)
                {
                    var h = resultHandler.HandleResult(context, info, result);

                    if (h != null)
                    {
                        h.Subscribe(u => { }, () => context.OnCompleted());
                        return;
                    }
                }
            }

            context.OnCompleted();
        }

        void InvocationException(IKayakContext context, InvocationInfo info, Exception exception)
        {
            foreach (var exceptionHandler in ExceptionHandlers)
            {
                var h = exceptionHandler.HandleException(context, info, exception);

                if (h != null)
                {
                    h.Subscribe(u => { }, () => context.OnCompleted());
                    return;
                }
            }

            context.OnCompleted();
        }

        void InvocationResult(IKayakContext context, InvocationInfo info, object result)
        {
            context.Items[InvocationResultContextKey] = result;
        }
    }
}
