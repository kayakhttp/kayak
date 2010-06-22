using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using System.IO;

namespace Kayak
{
    public static class Extensions
    {
        public static Coroutine AsCoroutine(this IEnumerable<object> iteratorBlock)
        {
            return new Coroutine(iteratorBlock.GetEnumerator());
        }

        internal static IObservable<T> EndWithDefault<T>(this IObservable<T> source)
        {
            return new EndWithDefaultSubject<T>(source);
        }


        public static IObservable<int> ReadAsync(this Stream s, byte[] buffer, int offset, int count)
        {
            return Observable.FromAsyncPattern<byte[], int, int, int>(s.BeginRead, s.EndRead)(buffer, offset, count).Repeat(1);
        }

        public static IObservable<Unit> WriteAsync(this Stream s, byte[] buffer, int offset, int count)
        {
            return Observable.FromAsyncPattern<byte[], int, int>(s.BeginWrite, s.EndWrite)(buffer, offset, count).Repeat(1);
        }

        //public static Func<byte[], int, int, IObservable<int>> Read(this Stream s)
        //{
        //    return Observable.FromAsyncPattern<byte[], int, int, int>(s.BeginRead, s.EndRead);
        //}

        //public static IObservable<int> ReadAsync(this Stream s, byte[] buffer, int offset, int count, Action<int> bytesRead)
        //{
        //    Console.WriteLine("Read async!");
        //    return new DeferredObservable<int>(() => s.Read()(buffer, offset, count).Do(bytesRead));
        //    Console.WriteLine("Done reading async.");
        //}
    }

    class EndWithDefaultSubject<T> : ISubject<T>
    {
        #region IObserver<T> Members
        
        IDisposable sx;
        ObserverCollection<T> observers;

        public EndWithDefaultSubject(IObservable<T> source)
        {
            sx = source.Subscribe(this);
            observers = new ObserverCollection<T>();
        }

        public void OnCompleted()
        {
            observers.Next(default(T));
            observers.Completed();
        }

        public void OnError(Exception exception)
        {
            observers.Error(exception);
        }

        public void OnNext(T value)
        {
            observers.Next(value);
        }

        #endregion

        #region IObservable<T> Members

        public IDisposable Subscribe(IObserver<T> observer)
        {
            return observers.Add(observer);
        }

        #endregion
    }

}
