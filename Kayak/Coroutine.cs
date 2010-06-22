using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Concurrency;
using System.Reflection;

namespace Kayak
{
    public class Coroutine : ISubject<object>
    {
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
			Continue();
        }

        public void OnError(Exception exception)
        {
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
			Continue();
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
                observers.Error(e);
            }

            if (continues)
            {
                try
                {
                    var value = continuation.Current;
					
					// ideally, this class (Coroutine) would be declared as such:
					// Coroutine<T> : ISubject<T> (where ISubject<T> : IObservable<out T>, IObserver<in T>)
					
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
                observers.Completed();
            }
	    }
		
		class ObserverShim<T> : IObserver<T>
		{
			internal Coroutine coroutine;
			
			public void OnNext (T value)
			{
				coroutine.OnNext(value);
			}
			
			
			public void OnError (Exception exception)
			{
				coroutine.OnError(exception);
			}
			
			
			public void OnCompleted ()
			{
				coroutine.OnCompleted();
			}
			
			public ObserverShim(Coroutine c)
			{
				coroutine = c;
			}
		}
    }
}
