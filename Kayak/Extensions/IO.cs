using System;
using System.Disposables;
using System.IO;

namespace Kayak
{
    public static partial class Extensions
    {
        public static IObservable<int> ReadAsync(this Stream s, byte[] buffer, int offset, int count)
        {
            return new AsyncOperation<int>((cb, st) => s.BeginRead(buffer, offset, count, cb, st), s.EndRead);
            //return Observable.FromAsyncPattern<byte[], int, int, int>(s.BeginRead, s.EndRead)(buffer, offset, count).Repeat(1);
        }

        public static IObservable<Unit> WriteAsync(this Stream s, byte[] buffer)
        {
            return s.WriteAsync(buffer, 0, buffer.Length);
        }

        public static IObservable<Unit> WriteAsync(this Stream s, byte[] buffer, int offset, int count)
        {
            return new AsyncOperation<Unit>((c, st) => s.BeginWrite(buffer, offset, count, c, st), iasr => { s.EndWrite(iasr); return new Unit(); });
            //return Observable.FromAsyncPattern<byte[], int, int>(s.BeginWrite, s.EndWrite)(buffer, offset, count).Repeat(1);
        }
    }

    // Observable.FromAsyncPattern schedules the callback on the ThreadPoolScheduler. Lame! Why doesn't
    // it allow the underlying async mechanism to determine what thread the callback comes in on?
    // 
    // And so we roll our own.

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

        //~AsyncOperation()
        //{
        //    Console.WriteLine("AsyncOperation deallocd");
        //}
    }
}
