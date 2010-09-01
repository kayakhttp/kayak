using System;
using System.Collections.Generic;
using System.Linq;
using System.Disposables;

namespace Kayak
{
    /// <summary>
    /// An observable that enumerates an enumerator.
    /// 
    /// Coroutine yields whatever the enumerator returns, except
    /// if it returns an obserable, it subscribes to it, yields an error if
    /// the observable yields an error, and only continues enumerating after the observable completes.
    /// 
    /// Very handy for writing asynchronous code using iterator blocks. Simply yield
    /// obserables that complete after the operation is complete (and possibly assign
    /// some resultant value to a variable in your local scope).
    /// </summary>
    public class Coroutine<T> : IObservable<T>
    {
        IObserver<T> observer;
        IEnumerator<object> continuation;
        IDisposable subscription;

        public Coroutine(IEnumerator<object> continuation)
        {
            this.continuation = continuation;
        }

        internal void OnCompleted()
        {
            if (subscription != null)
            {
                subscription.Dispose();
                subscription = null;
            }
            Continue();
        }

        void Complete()
        {
            //Console.WriteLine("Coroutine " + continuation + " completed.");
            observer.OnCompleted();
        }

        internal void OnError(Exception exception)
        {
            //Trace.Write("Coroutine error: " + exception.Message);
            //Trace.Write(exception.StackTrace);
            //Console.WriteLine("OnError!");
            observer.OnError(exception);
        }

        public IDisposable Subscribe(IObserver<T> observer)
        {
            if (observer == null)
                throw new ArgumentNullException("observer");

            if (this.observer != null)
                throw new InvalidOperationException("Coroutine already has observer.");

            this.observer = observer;

            Continue();

            //Console.WriteLine("Adding observer to continuation " + continuation);
            return Disposable.Create(() =>
            {
                if (subscription != null)
                    subscription.Dispose();

                this.observer = null;
            });
        }

        void Continue()
        {
            var continues = false;

            try
            {
                continues = continuation.MoveNext();
            }
            catch (Exception e)
            {
                OnError(e);
                return;
            }

            if (!continues)
            {
                Complete();
                return;
            }

            try
            {
                var value = continuation.Current;

                // really i want to type
                //
                // if (value is IObservable<object>)
                //     (value as IObservable<object>).Subscribe(o => { }, e => OnError, OnCompleted)
                // else
                //     ...

                // unfortunately i can't get the variant versions of IObservable/IObserver
                // to work with mono! i can't call IObservable<T>.Subscribe() with
                // an IObserver<object>, so I bake up a crazy shim type which forwards
                // Exception and Complete messages to us. Values are discarded; they're 
                // only meaningful to the enclosing scope, which is opaque to this class.

                Type observableInterface = null;

                if (value != null)
                    observableInterface = value.GetType().GetInterface("IObservable`1");

                if (observableInterface != null)
                {
                    var genericArg = observableInterface.GetGenericArguments()[0];
                    var shimType = typeof(ObserverShim<,>).MakeGenericType(genericArg, typeof(T));

                    var shimConstructor = shimType.GetConstructor(new Type[] { typeof(Coroutine<T>) });
                    var shim = shimConstructor.Invoke(new object[] { this });

                    var genericSubscribe = observableInterface.GetMethod("Subscribe");
                    subscription = (IDisposable)genericSubscribe.Invoke(value, new object[] { shim });
                }
                else
                {
                    if (value is T)
                        observer.OnNext((T)value);

                    Continue();
                }

            }
            catch (Exception e)
            {
                OnError(e);
            }
	    }
    }

    // this must be outside Coroutine<T> or else their generic types will get tangled.
    class ObserverShim<T0, T1> : IObserver<T0>
    {
        Coroutine<T1> coroutine;

        public void OnNext(T0 value)
        {
            // discard!
        }

        public void OnError(Exception exception)
        {
            coroutine.OnError(exception);
        }

        public void OnCompleted()
        {
            coroutine.OnCompleted();
        }

        public ObserverShim(Coroutine<T1> c)
        {
            coroutine = c;
        }
    }

    public static partial class Extensions
    {
        public static Coroutine<T> AsCoroutine<T>(this IEnumerable<object> iteratorBlock)
        {
            return new Coroutine<T>(iteratorBlock.GetEnumerator());
        }
    }
}
