using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Kayak
{
    public class Coroutine<T>
    {
        IEnumerator<object> continuation;
        AsyncResult<T> asyncResult;
        TaskScheduler scheduler;

        public Coroutine(IEnumerator<object> continuation, TaskScheduler scheduler)
        {
            this.continuation = continuation;
            this.scheduler = scheduler;
        }

        public IAsyncResult BeginInvoke(AsyncCallback cb, object state)
        {
            asyncResult = new AsyncResult<T>(cb, state);

            Continue(true);

            return asyncResult;
        }

        public T EndInvoke(IAsyncResult result)
        {
            //Console.WriteLine("Coroutine.EndInvoke");
            AsyncResult<T> ar = (AsyncResult<T>)result;
            continuation.Dispose();
            return ar.EndInvoke();
        }

        void Continue(bool sync)
        {
            //Console.WriteLine("Continuing!");
            var continues = false;

            try
            {
                continues = continuation.MoveNext();
            }
            catch (Exception e)
            {
                //Console.WriteLine("Exception during MoveNext.");
                asyncResult.SetAsCompleted(e, sync);
                return;
            }

            if (!continues)
            {
                //Console.WriteLine("Continuation does not continue.");
                asyncResult.SetAsCompleted(null, sync);
                return;
            }

            try
            {
                var value = continuation.Current;

                var task = value as Task;

                if (task != null)
                {
                    //Console.WriteLine("Will continue after Task.");
                    task.ContinueWith(t => {
                        bool synch = ((IAsyncResult)t).CompletedSynchronously;

                        if (false && t.IsFaulted) // disabled, iterator blocks must always remember to check for exceptions
                        {
                            //Console.WriteLine("Exception in Task.");
                            //Console.Out.WriteException(t.Exception);
                            asyncResult.SetAsCompleted(t.Exception, sync);
                        }
                        else
                        {
                            //Console.WriteLine("Continuing after Task.");
                            Continue(sync);
                        }
                    }, CancellationToken.None, TaskContinuationOptions.None, scheduler);
                }
                else
                {
                    if (value is T)
                    {
                        //Console.WriteLine("Completing with value.");
                        asyncResult.SetAsCompleted((T)value, sync);
                        return;
                    }

                    //Console.WriteLine("Continuing, discarding value.");
                    Continue(sync);
                }
            }
            catch (Exception e)
            {
                asyncResult.SetAsCompleted(e, sync);
                return;
            }
        }
    }

    public static partial class Extensions
    {
        public static Task<T> AsCoroutine<T>(this IEnumerable<object> iteratorBlock)
        {
            return iteratorBlock.AsCoroutine<T>(TaskScheduler.Current);
        }
        
        public static Task<T> AsCoroutine<T>(this IEnumerable<object> iteratorBlock, TaskScheduler scheduler)
        {
            return iteratorBlock.AsCoroutine<T>(TaskCreationOptions.None, scheduler);
        }

        public static Task<T> AsCoroutine<T>(this IEnumerable<object> iteratorBlock, TaskCreationOptions opts, TaskScheduler scheduler)
        {
            var coroutine = new Coroutine<T>(iteratorBlock.GetEnumerator(), scheduler);

            var cs = new TaskCompletionSource<T>(opts);

            coroutine.BeginInvoke(iasr => {
                try
                {
                    cs.SetResult(coroutine.EndInvoke(iasr));
                }
                catch(Exception e)
                {
                    cs.SetException(e);
                }
            }, null);

            return cs.Task;
        }

        public static Task<T> CreateCoroutine<T>(this IEnumerable<object> iteratorBlock)
        {
            return iteratorBlock.CreateCoroutine<T>(TaskScheduler.Current);
        }

        public static Task<T> CreateCoroutine<T>(this IEnumerable<object> iteratorBlock, TaskScheduler scheduler)
        {
            var taskFactory = new TaskFactory(scheduler);
            var tcs = new TaskCompletionSource<T>();
            
            taskFactory.StartNew(() => iteratorBlock.AsCoroutine<T>().ContinueWith(t
                =>
                {
                    if (t.IsFaulted)
                        tcs.SetException(t.Exception.InnerExceptions);
                    else
                        tcs.SetResult(t.Result);
                }));

            return tcs.Task;
        }

        public static Task<object> AsTaskWithValue(this Task task)
        {
            // well, this is significantly less painful than the observable version...

            var taskType = task.GetType();

            if (!taskType.IsGenericType) return null;

            var tcs = new TaskCompletionSource<object>();
            task.ContinueWith(t =>
            {
                if (task.IsFaulted)
                    tcs.SetException(task.Exception);
                else
                    tcs.SetResult(taskType.GetProperty("Result").GetGetMethod().Invoke(task, null));
            });
            return tcs.Task;
        }
    }
}
