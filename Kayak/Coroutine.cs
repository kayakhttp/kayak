//using System;
//using System.Collections.Generic;
//using System.Disposables;
//using System.Linq;

//namespace Kayak
//{
//    /// <summary>This is an observable that enumerates an enumerator.
//    /// 
//    /// Coroutine yields any object the enumerator yields, except if it returns 
//    /// an observable, it subscribes to it, yields an error if the observable 
//    /// yields an error, and only continues enumerating after the observable 
//    /// completes (the values in the observable sequence are discarded by Coroutine).
//    /// 
//    /// Coroutine allows you to easily write asynchronous code using iterator 
//    /// blocks declared to return `IEnumerable&lt;object&gt;`. To perform an asynchronous
//    /// operation, yield an observable which completes after the operation is complete. If the
//    /// operation returns one or more values, you can use the `Observable.Do` method to capture
//    /// those values and do something with them in your local scope.
//    /// </summary>
//    /// <remarks>
//    /// TODO (maybe): Add an option to prevent exceptions from being passed on to observers. In this
//    /// way, users can handle exceptions locally. Unfortunately, if you don't explicitly handle
//    /// exceptions when this option is set, your program will just continue, possibly leading to 
//    /// strange behavior, because there's no way for coroutine to know if the exception was handled. Is there?
//    /// Seems like a pretty hacky, horrible abuse of C#.
//    /// </remarks>
//    public class Coroutine<T> : IObservable<T>
//    {
//        IObserver<T> observer;
//        IEnumerator<object> continuation;
//        IDisposable subscription;

//        /// <summary>
//        /// Constructs a coroutine using the given enumerator. Usually this will be an enumerator of
//        /// an iterator block, which represents a continuation.
//        /// </summary>
//        public Coroutine(IEnumerator<object> continuation)
//        {
//            this.continuation = continuation;
//        }

//        internal void OnCompleted()
//        {
//            if (subscription != null)
//            {
//                subscription.Dispose();
//                subscription = null;
//            }
//            Continue();
//        }

//        void Complete()
//        {
//            //Console.WriteLine("Coroutine " + continuation + " completed.");
//            observer.OnCompleted();
//        }

//        internal void OnError(Exception exception)
//        {
//            //Trace.Write("Coroutine error: " + exception.Message);
//            //Trace.Write(exception.StackTrace);
//            //Console.WriteLine("OnError!");
//            observer.OnError(exception);
//        }

//        public IDisposable Subscribe(IObserver<T> observer)
//        {
//            if (observer == null)
//                throw new ArgumentNullException("observer");

//            if (this.observer != null)
//                throw new InvalidOperationException("Coroutine already has observer.");

//            this.observer = observer;

//            Continue();

//            //Console.WriteLine("Adding observer to continuation " + continuation);
//            return Disposable.Create(() =>
//            {
//                if (subscription != null)
//                    subscription.Dispose();

//                this.observer = null;
//            });
//        }

//        void Continue()
//        {
//            var continues = false;

//            try
//            {
//                continues = continuation.MoveNext();
//            }
//            catch (Exception e)
//            {
//                OnError(e);
//                return;
//            }

//            if (!continues)
//            {
//                Complete();
//                return;
//            }

//            try
//            {
//                var value = continuation.Current;

//                var observable = value.AsObservable();

//                if (observable != null)
//                    observable.Subscribe(Observer.Create<object>(o => { }, OnError, OnCompleted));
//                else
//                {
//                    // TODO test (or amend) this behavior
//                    if (value is T /* || type.IsAssignableFrom(value.GetType()) */) // maybe?
//                        observer.OnNext((T)value);

//                    Continue();
//                }

//            }
//            catch (Exception e)
//            {
//                OnError(e);
//            }
//        }
//    }

//    public static partial class Extensions
//    {
//        public static IObservable<object> AsObservable(this object value)
//        {
//            if (value == null)
//                throw new ArgumentNullException("value");

//            // really i want to type
//            //
//            // if (value is IObservable<object>)
//            //     (value as IObservable<object>).Subscribe(o => { }, e => OnError, OnCompleted)
//            // else
//            //     ...

//            // unfortunately i can't get the variant versions of IObservable/IObserver
//            // to work with mono! i can't call IObservable<T>.Subscribe() with
//            // an IObserver<object>, so I bake up a crazy shim type which forwards
//            // Exception and Complete messages to us. Values are discarded; they're 
//            // only meaningful to the enclosing scope, which is opaque to this class.

//            Type observableInterface = null;

//            if (value != null)
//                observableInterface = value.GetType().GetInterface("IObservable`1");

//            if (observableInterface == null)
//                return null;

//            return Observable.CreateWithDisposable<object>(o =>
//            {
//                var genericArg = observableInterface.GetGenericArguments()[0];
//                var shimType = typeof(ObserverShim<>).MakeGenericType(genericArg);

//                var shimConstructor = shimType.GetConstructor(new Type[] { typeof(IObserver<object>) });
//                var shim = shimConstructor.Invoke(new object[] { o });

//                var genericSubscribe = observableInterface.GetMethod("Subscribe");
//                return (IDisposable)genericSubscribe.Invoke(value, new object[] { shim });
//            });
//        }
//    }

//    // this must be outside Coroutine<T> or else their generic types will get tangled.
//    class ObserverShim<T> : IObserver<T>
//    {
//        IObserver<object> observer;

//        public void OnNext(T value)
//        {
//            observer.OnNext(value);
//        }

//        public void OnError(Exception exception)
//        {
//            observer.OnError(exception);
//        }

//        public void OnCompleted()
//        {
//            observer.OnCompleted();
//        }

//        public ObserverShim(IObserver<object> observer)
//        {
//            this.observer = observer;
//        }
//    }

//    public static partial class CoroutineExtensions
//    {
//        /// <summary>
//        /// Creates a Coroutine over an `IEnumerable&lt;object&gt;` defined using the C# iterator
//        /// block syntax. The iterator block must be an enumerator of type `object`. You can `yield return` 
//        /// objects of any type from the iterator block (including observables, which are handled as described
//        /// above), but the observable that this method returns will only yield values yielded by the iterator block
//        /// which can be cast to the type `T`.
//        /// </summary>
//        public static IObservable<T> AsCoroutine<T>(this IEnumerable<object> iteratorBlock)
//        {
//            return new Coroutine<T>(iteratorBlock.GetEnumerator());
//        }
//    }
//}
