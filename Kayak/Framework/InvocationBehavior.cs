using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using LitJson;
using System.IO;

namespace Kayak.Framework
{
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

    // not thread-safe!
    public class KayakInvocationBehavior : IInvocationBehavior
    {
        public Func<IKayakContext, MethodInfo> Map { get; set; }
        public List<IInvocationArgumentBinder> Binders { get; private set; }
        public List<IInvocationResultHandler> ResultHandlers { get; private set; }
        public List<IInvocationExceptionHandler> ExceptionHandlers { get; private set; }

        static string InvocationResultContextKey = "InvocationResultContextKey";

        public KayakInvocationBehavior()
        {
            Binders = new List<IInvocationArgumentBinder>();
            ResultHandlers = new List<IInvocationResultHandler>();
            ExceptionHandlers = new List<IInvocationExceptionHandler>();

            Binders.Add(new HeaderBinder());
            ExceptionHandlers.Add(new DefaultExceptionHandler());
        }

        public IObservable<InvocationInfo> GetBinder(IKayakContext context)
        {
            return Bind(context).AsCoroutine<InvocationInfo>();
        }

        IEnumerable<object> Bind(IKayakContext context)
        {
            InvocationInfo info = new InvocationInfo();

            info.Method = Map(context);
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
