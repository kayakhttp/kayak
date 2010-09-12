using System;
using System.Collections.Generic;
using System.Disposables;
using System.Linq;

namespace Kayak
{
    // Observable.FromAsyncPattern schedules the callback on the ThreadPoolScheduler. Lame! Why doesn't
    // it allow the underlying async mechanism to determine what thread the callback comes in on?
    // 
    // And so we roll our own
    class AsyncOperation<T> : IObservable<T>
    {
        Func<AsyncCallback, object, IAsyncResult> begin;
        Func<IAsyncResult, T> end;

        public AsyncOperation(Func<AsyncCallback, object, IAsyncResult> begin, Func<IAsyncResult, T> end)
        {
            this.begin = begin;
            this.end = end;
        }

        #region IObservable<T> Members

        public IDisposable Subscribe(IObserver<T> observer)
        {
            IAsyncResult asyncResult = null;

            AsyncCallback callback = (iasr) =>
            {
                //Console.WriteLine("Got async callback");
                T value = default(T);
                try
                {
                    value = end(iasr);
                }
                catch (Exception e)
                {
                    observer.OnError(e);
                    return;
                }
                observer.OnNext(value);
                observer.OnCompleted();
            };

            asyncResult = begin(callback, null);

            return Disposable.Empty;
        }

        #endregion
    }


    // A mechanism which allows you to transform an observable of one type into an
    // observable of another type in an asynchronous manner.
    // Not thread-safe.
    class AsyncTransform<TIn, TOut> : IConnectableObservable<TOut>
    {
        IObservable<TIn> source;
        Func<TIn, IObservable<TOut>> transform;
        Action<TIn, Exception> transformExceptionHandler;
        ISubject<TOut> subject;

        public AsyncTransform(IObservable<TIn> source,
            Func<TIn, IObservable<TOut>> transform,
            Action<TIn, Exception> transformExceptionHandler)
        {
            this.source = source;
            this.transform = transform;
            this.transformExceptionHandler = transformExceptionHandler;
            this.subject = new Subject<TOut>();
        }

        public IDisposable Connect()
        {
            var ss = source.Subscribe((TIn o) =>
            {
                var transformed = transform(o);
                transformed
                    .Catch<TOut, Exception>(e => { transformExceptionHandler(o, e); return null; })
                    .Subscribe(ob => subject.OnNext(ob));
            });
            // we don't care about errors or completed on the source observable right now.

            return Disposable.Create(() =>
            {
                ss.Dispose();
                subject.OnCompleted();
            });
        }

        public IDisposable Subscribe(IObserver<TOut> observer)
        {
            return subject.Subscribe(observer);
        }
    }
}
