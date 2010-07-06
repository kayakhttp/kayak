using System;
using System.Collections.Generic;
using System.Linq;

namespace Kayak
{
    /// <summary>
    /// An observable that enumerates an enumerator. Call the Start() method to begin enumeration.
    /// 
    /// Coroutine yields whatever the enumerator returns, except
    /// if the iterator block yields an obserable, it subscribes to it, yields whatever values or errors
    /// the observable yields, and only continues enumerating after the observable completes. If the 
    /// observable returned by the enumerator was an instance of Coroutine, its Start() method is 
    /// called after subscribing.
    /// 
    /// Very handy for writing asynchronous code using iterator blocks. Simply yield
    /// obserables that complete after the operation is complete (and possibly assign
    /// a value to some variable in your local scope).
    /// </summary>
    public class Coroutine : ISubject<object>
    {
        // ideally, Coroutine would be declared as:
        // 
        // Coroutine<T> : ISubject<T>
        //
        // but this is only useful if ISubject is co- and contra-variant
        // (where ISubject<T> : IObservable<out T>, IObserver<in T>).
        // 
        // for now we fake it by introducing a shim type which casts to object.

        public static Action<Action> Trampoline;

        ObserverCollection<object> observers;
        IEnumerator<object> continuation;
        bool started, completed, subscribing;
        IDisposable subscription;

        public bool Started { get { return started; } }
        public bool Completed { get { return completed; } }

        public Coroutine(IEnumerator<object> continuation)
        {
            this.continuation = continuation;
            this.observers = new ObserverCollection<object>();
        }

        public void OnCompleted()
        {
            if (!subscribing)
            {
                subscription.Dispose();
                subscription = null;
            }
            BeginContinue();
        }

        public void OnError(Exception exception)
        {
            Trace.Write("Coroutine error: " + exception.Message);
            Trace.Write(exception.StackTrace);
            observers.Error(exception);
            observers.Completed();
        }

        public void OnNext(object value)
        {
            observers.Next(value);
        }

        public IDisposable Subscribe(IObserver<object> observer)
        {
            if (observer == null)
                throw new ArgumentNullException("observer");

            if (completed)
                throw new Exception("Coroutine already completed!");

            return observers.Add(observer);
        }

        public void Start()
        {
            started = true;
            BeginContinue();
        }

        void BeginContinue()
        {
            //Scheduler.CurrentThread.EnsureTrampoline(Continue);
            Trace.Write("Queuing continuation " + continuation);
            if (Trampoline != null)
                Trampoline(Continue);
            else
                Continue();
        }

        void Continue()
        {
            var continues = false;

            try
            {
                Trace.Write("Executing continuation " + continuation);
                continues = continuation.MoveNext();
            }
            catch (Exception e)
            {
                observers.Error(e);
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
					
					var observableInterface = value.GetType().GetInterfaces()
						.FirstOrDefault(i => i.GetGenericTypeDefinition().Equals(typeof(IObservable<>)));

                    if (observableInterface != null)
                    {
						var genericArg = observableInterface.GetGenericArguments()[0];
						var shimType = typeof(ObserverShim<>).MakeGenericType(genericArg);
						var shim = shimType.GetConstructor(new Type[] { typeof(Coroutine) } ).Invoke(new object[] { this });
						var genericSubscribe = observableInterface.GetMethod("Subscribe");

                        subscribing = true;
						subscription = (IDisposable)genericSubscribe.Invoke(value, new object[] { shim });
                        subscribing = false;

                        if (value is Coroutine)
                            (value as Coroutine).Start();
                    }
                    else
                    {
                        observers.Next(value);
                    }
                }
                catch (Exception e)
                {
                    observers.Error(e);
                }
            }
            else
            {
                Trace.Write("Coroutine " + continuation + " completed.");
                //Console.WriteLine();
                completed = true;
                observers.Completed();
            }
	    }
		
		class ObserverShim<T> : IObserver<T>
		{
			internal Coroutine coroutine;
			
			public void OnNext(T value)
			{
				coroutine.OnNext(value);
			}
			
			public void OnError(Exception exception)
			{
				coroutine.OnError(exception);
			}
			
			public void OnCompleted()
			{
				coroutine.OnCompleted();
			}
			
			public ObserverShim(Coroutine c)
			{
				coroutine = c;
			}
		}
    }

    public static partial class Extensions
    {
        public static Coroutine AsCoroutine(this IEnumerable<object> iteratorBlock)
        {
            return new Coroutine(iteratorBlock.GetEnumerator());
        }
    }
}
