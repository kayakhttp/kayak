using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;

namespace Kayak.Framework
{
    public interface IKayakInvocation : IObservable<object>
    {
        IKayakContext Context { get; }
        InvocationInfo Info { get; }
    }

    public class KayakInvocation : IKayakInvocation
    {
        public IKayakContext Context { get; private set; }
        public InvocationInfo Info { get; private set; }
        IObserver<object> observer;
        IObservable<InvocationInfo> bind;

        public KayakInvocation(IKayakContext context, IObservable<InvocationInfo> bind)
        {
            Context = context;
            this.bind = bind;
        }

        IEnumerable<object> Invoke()
        {
            yield return bind.Do(i => Info = i);

            if (Info.Method == null)
            {
                //Console.WriteLine("Method was null.");
                yield break;
            }

            object result = null;
            bool error = false;

            try
            {
                result = Info.Invoke();
            }
            catch (Exception e)
            {
                error = true;
                observer.OnError(e);
            }

            if (!error)
            {
                if (Info.Method.ReturnType != typeof(void))
                    observer.OnNext(result);

                observer.OnCompleted();
            }
        }

        public IDisposable Subscribe(IObserver<object> observer)
        {
            this.observer = observer;

            var invoke = Invoke().AsCoroutine();
            // context does not get error if an invoked method throws an error, 
            // only if something else weird happens.
            return invoke.Subscribe(o => { }, e => { Context.OnError(e); });
        }
    }
}
