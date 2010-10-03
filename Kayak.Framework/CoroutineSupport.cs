//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;

//namespace Kayak.Framework
//{
//    public static partial class Extensions
//    {
//        public static IObservable<Unit> HandleWithCoroutine(this IKayakContext context, Func<IKayakContext, IObservable<Unit>> handle)
//        {
//            var coroutine = context.GetCoroutine();

//            if (coroutine == null)
//                return null;

//            var info = context.GetInvocationInfo();
//            info.Result = null;

//            return Observable.CreateWithDisposable<Unit>(o => coroutine.Subscribe(
//                    r => info.Result = r,
//                    e =>
//                    {
//                        info.Exception = e;
//                        var handler = handle(context);

//                        if (handler != null)
//                            handler.Subscribe(o);
//                        else
//                            o.OnError(e);
//                    },
//                    () =>
//                    {
//                        var handler = handle(context);

//                        if (handler != null)
//                            handler.Subscribe(o);
//                        else
//                            o.OnCompleted();
//                    }));
//        }
//        public static IObservable<object> GetCoroutine(this IKayakContext context)
//        {
//            var info = context.GetInvocationInfo();

//            if (!(info.Result is IEnumerable<object>))
//                return null;

//            var continuation = info.Result as IEnumerable<object>;

//            return continuation.AsCoroutine<object>();
//        }
//    }
//}
