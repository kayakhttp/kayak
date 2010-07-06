using System;
using System.Disposables;
using System.IO;

namespace Kayak
{
    static class Trace
    {
        public static void Write(string format, params object[] args)
        {
            //StackTrace stackTrace = new StackTrace();
            //StackFrame stackFrame = stackTrace.GetFrame(1);
            //MethodBase methodBase = stackFrame.GetMethod();
            //Console.WriteLine("[thread " + Thread.CurrentThread.ManagedThreadId + ", " + methodBase.DeclaringType.Name + "." + methodBase.Name + "] " + string.Format(format, args));
        }
    }

    public static partial class Extensions
    {
        public static void WriteException(this TextWriter writer, Exception exception)
        {
            writer.WriteLine("____________________________________________________________________________");

            writer.WriteLine("[{0}] {1}", exception.GetType().Name, exception.Message);
            writer.WriteLine(exception.StackTrace);
            writer.WriteLine();

            for (Exception e = exception.InnerException; e != null; e = e.InnerException)
            {
                writer.WriteLine("Caused by:\r\n[{0}] {1}", e.GetType().Name, e.Message);
                writer.WriteLine(e.StackTrace);
                writer.WriteLine();
            }
        }

        public static IObservable<int> ReadAsync(this Stream s, byte[] buffer, int offset, int count)
        {
            return new AsyncOperation<int>((cb, st) => s.BeginRead(buffer, offset, count, cb, st), s.EndRead);
            //return Observable.FromAsyncPattern<byte[], int, int, int>(s.BeginRead, s.EndRead)(buffer, offset, count).Repeat(1);
        }

        public static IObservable<Unit> WriteAsync(this Stream s, byte[] buffer, int offset, int count)
        {
            return new AsyncOperation<Unit>((c, st) => s.BeginWrite(buffer, offset, count, c, st), iasr => { s.EndWrite(iasr); return new Unit(); });
            //return Observable.FromAsyncPattern<byte[], int, int>(s.BeginWrite, s.EndWrite)(buffer, offset, count).Repeat(1);
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


    //
    public class AsyncOperation<T> : IObservable<T>
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
