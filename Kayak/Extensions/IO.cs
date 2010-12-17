using System;
using System.Net.Sockets;
using Owin;
using System.Threading.Tasks;

namespace Kayak
{
    public static partial class Extensions
    {
        //internal static IObservable<int> SendAsync(this Socket s, byte[] buffer, int offset, int count)
        //{
        //    return new AsyncOperation<int>(
        //        (c, st) => s.BeginSend(buffer, offset, count, SocketFlags.None, c, st),
        //        iasr => { return s.EndSend(iasr); });
        //}

        internal static Task<int> SendAsync(this Socket s, byte[] buffer, int offset, int count)
        {
            var tcs = new TaskCompletionSource<int>();

            s.BeginSend(buffer, offset, count, SocketFlags.None, iasr =>
            {
                try
                {
                    tcs.SetResult(s.EndSend(iasr));
                }
                catch (Exception e)
                {
                    tcs.SetException(e);
                }
            }, null);

            return tcs.Task;
        }

        //internal static IObservable<Unit> SendFileAsync(this Socket s, string file)
        //{
        //    return new AsyncOperation<Unit>(
        //        (c, st) => s.BeginSendFile(file, c, st),
        //        iasr => { s.EndSendFile(iasr); return new Unit(); });
        //}

        internal static Task SendFileAsync(this Socket s, string file)
        {
            var tcs = new TaskCompletionSource<int>();

            s.BeginSendFile(file, iasr =>
            {
                try
                {
                    s.EndSendFile(iasr);
                }
                catch (Exception e)
                {
                    tcs.SetException(e);
                }
            }, null);

            return tcs.Task;
        }

        //internal static IObservable<int> ReceiveAsync(this Socket s, byte[] buffer, int offset, int count)
        //{
        //    return new AsyncOperation<int>(
        //        (c, st) => s.BeginReceive(buffer, offset, count, SocketFlags.None, c, st),
        //        iasr => { return s.EndReceive(iasr); });
        //}

        internal static Task<int> ReceiveAsync(this Socket s, byte[] buffer, int offset, int count)
        {
            var tcs = new TaskCompletionSource<int>();

            s.BeginReceive(buffer, offset, count, SocketFlags.None, iasr =>
                {
                    try
                    {
                        tcs.SetResult(s.EndReceive(iasr));
                    }
                    catch (Exception e)
                    {
                        tcs.SetException(e);
                    }
                }, null);

            return tcs.Task;
        }

        //internal static IObservable<Socket> AcceptSocketAsync(this TcpListener listener)
        //{
        //    return new AsyncOperation<Socket>(
        //        (c, st) => listener.BeginAcceptSocket(c, st),
        //        iasr => listener.EndAcceptSocket(iasr));
        //}

        internal static Task<Socket> AcceptSocketAsync(this TcpListener listener)
        {
            var tcs = new TaskCompletionSource<Socket>();

            listener.BeginAcceptSocket(iasr =>
                {
                    try
                    {
                        tcs.SetResult(listener.EndAcceptSocket(iasr));
                    }
                    catch (Exception e)
                    {
                        tcs.SetException(e);
                    }
                }, null);

            return tcs.Task;
        }

        //public static IObservable<int> ReadBodyAsync(this IRequest request, byte[] buffer, int offset, int count)
        //{
        //    return new AsyncOperation<int>(
        //        (c, s) => request.BeginReadBody(buffer, offset, count, c, s),
        //        iasr => request.EndReadBody(iasr));
        //}

        public static Task<int> ReadBodyAsync(this IRequest request, byte[] buffer, int offset, int count)
        {
            var tcs = new TaskCompletionSource<int>();

            request.BeginReadBody(buffer, offset, count, iasr => {
                try
                {
                    tcs.SetResult(request.EndReadBody(iasr));
                }
                catch (Exception e)
                {
                    tcs.SetException(e);
                }
            }, null);

            return tcs.Task;
        }
    }

    // Observable.FromAsyncPattern schedules the callback on the ThreadPoolScheduler. Lame! Why doesn't
    // it allow the underlying async mechanism to determine what thread the callback comes in on?
    // 
    // And so we roll our own.
    //class AsyncOperation<T> : IObservable<T>
    //{
    //    Func<AsyncCallback, object, IAsyncResult> begin;
    //    Func<IAsyncResult, T> end;

    //    public AsyncOperation(Func<AsyncCallback, object, IAsyncResult> begin, Func<IAsyncResult, T> end)
    //    {
    //        this.begin = begin;
    //        this.end = end;
    //    }

    //    #region IObservable<T> Members

    //    public IDisposable Subscribe(IObserver<T> observer)
    //    {
    //        IAsyncResult asyncResult = null;

    //        AsyncCallback callback = (iasr) =>
    //        {
    //            //Console.WriteLine("Got async callback");
    //            T value = default(T);
    //            try
    //            {
    //                value = end(iasr);
    //            }
    //            catch (Exception e)
    //            {
    //                observer.OnError(e);
    //                return;
    //            }
    //            observer.OnNext(value);
    //            observer.OnCompleted();
    //        };

    //        try
    //        {
    //            asyncResult = begin(callback, null);
    //        }
    //        catch (Exception e)
    //        {
    //            observer.OnError(e);
    //        }

    //        return Disposable.Empty;
    //    }

    //    #endregion
    //}
}
