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
    /// if the iterator block yields an obserable, it subscribes to it, yields an error if
    /// the observable yields and error, and only continues enumerating after the observable completes.
    /// 
    /// Very handy for writing asynchronous code using iterator blocks. Simply yield
    /// obserables that complete after the operation is complete (and possibly assign
    /// some resultant value to a variable in your local scope).
    /// </summary>
    public class Coroutine<T> : IObservable<T>
    {
        // ideally, Coroutine would be declared as:
        // 
        // Coroutine<T> : ISubject<T>
        //
        // but this is only useful if ISubject is co- and contra-variant
        // (where ISubject<T> : IObservable<out T>, IObserver<in T>).
        // 
        // for now we fake it by introducing a shim type which casts to object.

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
                //Trace.Write("Executing continuation " + continuation);
                continues = continuation.MoveNext();
            }
            catch (Exception e)
            {
                OnError(e);
                return;
            }

            if (continues)
            {
                try
                {
                    var value = continuation.Current;
					
                    // if we could support variance:

					// this way enumerator blocks could yield any sort of object, we would be able
					// to test for IObservable<T> (T would be covariant), and we could subscribe to it,
					// and subscribers to us could observe values produced by that observable.
					
					// but unfortunately we can't get the variant versions of IObservable/IObserver
					// to work with mono! so Coroutine is not generic, it implements ISubject<object>,
					// and we use a crazy shim type to observe any IObservable a block may yield,
					// and it forwards calls from that observable to us (with values cast to object).
					
					// bleh.

                    Type observableInterface = null;

                    if (value != null)
                        observableInterface = value.GetType().GetInterface("IObservable`1");

                    if (observableInterface == null)
                    {
                        if (value is T)
                            observer.OnNext((T)value);

                        Continue();
                    }
                    else if (observableInterface != null)
                    {
						var genericArg = observableInterface.GetGenericArguments()[0];
						var shimType = typeof(ObserverShim<,>).MakeGenericType(genericArg, typeof(T));
                        var shimConstructor = shimType.GetConstructor(new Type[] { typeof(Coroutine<T>) });
                        var shim = shimConstructor.Invoke(new object[] { this });
						var genericSubscribe = observableInterface.GetMethod("Subscribe");

						subscription = (IDisposable)genericSubscribe.Invoke(value, new object[] { shim });
                    }
                }
                catch (Exception e)
                {
                    OnError(e);
                }
            }
            else
            {
                Complete();
            }
	    }
    }

    class ObserverShim<T0, T1> : IObserver<T0>
    {
        Coroutine<T1> coroutine;

        public void OnNext(T0 value)
        {
            // discard
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
