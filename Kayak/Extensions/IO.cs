using System;
using System.Disposables;
using System.IO;
using System.Net.Sockets;

namespace Kayak
{
    public static partial class Extensions
    {
        public static IObservable<int> SendAsync(this Socket s, byte[] buffer, int offset, int count)
        {
            return new AsyncOperation<int>(
                (c, st) => s.BeginSend(buffer, offset, count, SocketFlags.None, c, st),
                iasr => { return s.EndSend(iasr); });
        }

        public static IObservable<int> SendFileAsync(this Socket s, string file)
        {
            return new AsyncOperation<int>(
                (c, st) => s.BeginSendFile(file, c, st),
                iasr => { s.EndSendFile(iasr); return 0; });
        }

        public static IObservable<int> ReceiveAsync(this Socket s, byte[] buffer, int offset, int count)
        {
            return new AsyncOperation<int>(
                (c, st) => s.BeginReceive(buffer, offset, count, SocketFlags.None, c, st),
                iasr => { return s.EndReceive(iasr); });
        }

        public static IObservable<Socket> AcceptSocketAsync(this TcpListener listener)
        {
            return new AsyncOperation<Socket>(
                (c, st) => listener.BeginAcceptSocket(c, st),
                iasr => listener.EndAcceptSocket(iasr));
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
