//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;
//using System.Reflection;
//using LitJson;
//using System.IO;

//namespace Kayak.Framework
//{


//    // not thread-safe!
//    public class KayakInvocationBehavior : IInvocationBehavior
//    {
//        public Func<IKayakContext, MethodInfo> Map { get; set; }
//        public List<IArgumentBinder> Binders { get; private set; }
//        public List<IResultHandler> ResultHandlers { get; private set; }
//        public List<IExceptionHandler> ExceptionHandlers { get; private set; }

//        static string InvocationResultContextKey = "InvocationResultContextKey";

//        public KayakInvocationBehavior()
//        {
//            Binders = new List<IArgumentBinder>();
//            ResultHandlers = new List<IResultHandler>();
//            ExceptionHandlers = new List<IExceptionHandler>();
//        }

//        public static KayakInvocationBehavior CreateDefaultBehavior()
//        {
//            return CreateDefaultBehavior(Assembly.GetCallingAssembly().GetTypes());
//        }

//        public static KayakInvocationBehavior CreateDefaultBehavior(Type[] types)
//        {
//            var behavior = new KayakInvocationBehavior();
//            behavior.Binders.Add(new HeaderBinder((s, t) => s.Coerce(t)));
//            behavior.ExceptionHandlers.Add(new DefaultExceptionHandler());
//            behavior.MapTypes(types);
//            behavior.AddFileSupport();
//            behavior.AddJsonSupport();
//            return behavior;
//        }

//        public IObservable<InvocationInfo> Bind()
//        {
//            return null;// BindInternal(context).AsCoroutine<InvocationInfo>();
//        }

//        IEnumerable<object> BindInternal(IKayakContext context)
//        {
//            InvocationInfo info = new InvocationInfo();

//            info.Method = Map(context);

//            if (info.Method == null)
//                yield break;

//            info.Target = Activator.CreateInstance(info.Method.DeclaringType);

//            var service = info.Target as KayakService;

//            if (service != null)
//                service.Context = context;

//            var parameterCount = info.Method.GetParameters().Length;
//            info.Arguments = new object[parameterCount];

//            if (parameterCount > 0)
//                foreach (var binder in Binders)
//                {
//                    binder.BindArgumentsFromHeaders(context, info);
//                    yield return binder.BindArgumentsFromBody(context, info);
//                }

//            yield return info;
//        }

//        public void Invoke(IObservable<object> invocation)
//        {
//            //invocation.Subscribe(
//            //    result => InvocationResult(context, result), 
//            //    e => InvocationException(context, e), 
//            //    () => InvocationCompleted(context));
//        }

//        void EndContext(IKayakContext context)
//        {
//            context.Response.End().Finally(() =>
//            {
//                Console.WriteLine("[{0}] {1} {2} {3} : {4} {5} {6}", DateTime.Now,
//                    context.Request.Verb, context.Request.Path, context.Request.HttpVersion,
//                    context.Response.HttpVersion, context.Response.StatusCode, context.Response.ReasonPhrase);
//            }).Subscribe();
//        }

//        void InvocationCompleted(IKayakContext context)
//        {
//            if (!context.Items.ContainsKey(InvocationResultContextKey))
//            {
//                EndContext(context);
//                return;
//            }

//            object result = context.Items[InvocationResultContextKey];

//            if (result is IEnumerable<object>)
//            {
//                object lastObject = null;

//                (result as IEnumerable<object>)
//                    .AsCoroutine<object>()
//                    .Subscribe(o => { lastObject = o; }, e => InvocationException(context, e), () => HandleResult(context, lastObject));
//            }
//            else
//                HandleResult(context, result);
//        }

//        void InvocationException(IKayakContext context, Exception exception)
//        {
//            var handler = GetExceptionHandler(context, exception);

//            if (handler != null)
//                handler.Subscribe(
//                    u => { },
//                    e => { },
//                    () => EndContext(context));
//            else
//                EndContext(context);
//        }

//        void InvocationResult(IKayakContext context, object result)
//        {
//            context.Items[InvocationResultContextKey] = result;
//        }

//        void HandleResult(IKayakContext context, object result)
//        {
//            var handler = GetResultHandler(context, result);

//            if (handler != null)
//                handler.Subscribe(u => { }, e => InvocationException(context, e), () => EndContext(context));
//            else
//                EndContext(context);
//        }

//        // this could be made public to support Comet-style shit.
//        // Context.Return(somevalue) { Context.GetBehavior().GetResultHandler(Context, somevalue); }
//        IObservable<Unit> GetResultHandler(IKayakContext context, object result)
//        {
//            var info = context.GetInvocationInfo();

//            foreach (var resultHandler in ResultHandlers)
//            {
//                var h = resultHandler.HandleResult(context, info, result);

//                if (h != null)
//                    return h;
//            }

//            return null;
//        }

//        IObservable<Unit> GetExceptionHandler(IKayakContext context, Exception e)
//        {
//            var info = context.GetInvocationInfo();

//            foreach (var exceptionHandler in ExceptionHandlers)
//            {
//                var h = exceptionHandler.HandleException(context, info, e);

//                if (h != null)
//                    return h;
//            }

//            return null;
//        }
//    }
//}
