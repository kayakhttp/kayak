using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.IO;

namespace Kayak
{
    public class SingleThreadedScheduler : TaskScheduler, IDisposable
    {
        [ThreadStatic]
        static bool isWorker;

        LinkedList<Task> queue;
        AutoResetEvent wh, stopWh;
        bool stopRequested;
        Thread worker;

        public SingleThreadedScheduler()
        {
            queue = new LinkedList<Task>();
            wh = new AutoResetEvent(false);
        }

        public void Start()
        {
            if (worker != null) throw new InvalidOperationException("Already started!");

            worker = new Thread(() => { Run2(); }) { IsBackground = true };
            worker.Start();
        }

        void Run2()
        {
            isWorker = true;

            while (true)
            {
                Console.WriteLine("Scheduler waiting.");
                wh.WaitOne();
                Console.WriteLine("Scheduler set.");

                try
                {
                    while (true)
                    {
                        Task item;

                        lock (queue)
                        {
                            if (queue.Count == 0)
                            {
                                Console.WriteLine("No more items in queue!");
                                break;
                            }

                            item = queue.First.Value;
                            queue.RemoveFirst();
                        }

                        Console.WriteLine("Executing task. " + item);
                        TryExecuteTask(item);
                        Console.WriteLine("'Done' executing.");
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine("Exception on worker!");
                    Console.Out.WriteException(e);
                }

                if (stopRequested)
                {
                    Console.WriteLine("Scheduler stopping!");
                    break;
                }
            }

            stopWh.Set();

            isWorker = false;
        }

        protected override IEnumerable<Task> GetScheduledTasks()
        {
            lock (queue)
                return queue.ToArray();
        }

        protected override void QueueTask(Task task)
        {
            lock (queue)
            {
                queue.AddLast(task);

                Console.WriteLine("Queued task, setting wh.");
                wh.Set();
            }
        }

        protected override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued)
        {
            Console.WriteLine("TryExecuteTaskInline: " + task + " taskWasPreviouslyQueued: " + taskWasPreviouslyQueued);

            if (!isWorker)
            {
                Console.WriteLine("TryExecuteTaskInline: wrong thread.");
                return false;
            }

            if (taskWasPreviouslyQueued)
            {
                Console.WriteLine("TryExecuteTaskInline: dequeuing task.");
                TryDequeue(task);
            }

            var result = TryExecuteTask(task);

            Console.WriteLine("TryExecuteTaskInline: returning " + result);
            return result;
        }

        protected override bool TryDequeue(Task task)
        {
            bool result = false;
            lock (queue)
                result = queue.Remove(task);

            Console.WriteLine("TryDequeue: " + result);
            return result;
        }

        public void Dispose()
        {
            stopRequested = true;
            stopWh = new AutoResetEvent(false);
            wh.Set();
            stopWh.WaitOne();
            stopWh.Close();
            wh.Close();
            worker.Join();
        }
    }


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
            Console.WriteLine("Coroutine.EndInvoke");
            AsyncResult<T> ar = (AsyncResult<T>)result;
            continuation.Dispose();
            return ar.EndInvoke();
        }

        void Continue(bool sync)
        {
            Console.WriteLine("Continuing!");
            var continues = false;

            try
            {
                continues = continuation.MoveNext();
            }
            catch (Exception e)
            {
                Console.WriteLine("Exception during MoveNext.");
                asyncResult.SetAsCompleted(e, sync);
                return;
            }

            if (!continues)
            {
                Console.WriteLine("Continuation does not continue.");
                asyncResult.SetAsCompleted(null, sync);
                return;
            }

            try
            {
                var value = continuation.Current;

                var task = value as Task;

                if (task != null)
                {
                    Console.WriteLine("Will continue after Task.");
                    task.ContinueWith(t => {
                        bool synch = ((IAsyncResult)t).CompletedSynchronously;

                        if (false && t.IsFaulted) // disabled, iterator blocks must always remember to check for exceptions
                        {
                            Console.WriteLine("Exception in Task.");
                            Console.Out.WriteException(t.Exception);
                            asyncResult.SetAsCompleted(t.Exception, sync);
                        }
                        else
                        {
                            Console.WriteLine("Continuing after Task.");
                            Continue(sync);
                        }
                    }, CancellationToken.None, TaskContinuationOptions.None, scheduler);
                }
                else
                {
                    if (value is T)
                    {
                        Console.WriteLine("Completing with value.");
                        asyncResult.SetAsCompleted((T)value, sync);
                        return;
                    }

                    Console.WriteLine("Continuing, discarding value.");
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

    // lifted from http://msdn.microsoft.com/en-us/magazine/cc163467.aspx
    internal class AsyncResultNoResult : IAsyncResult
    {
        // Fields set at construction which never change while 
        // operation is pending
        private readonly AsyncCallback m_AsyncCallback;
        private readonly Object m_AsyncState;

        // Fields set at construction which do change after 
        // operation completes
        private const Int32 c_StatePending = 0;
        private const Int32 c_StateCompletedSynchronously = 1;
        private const Int32 c_StateCompletedAsynchronously = 2;
        private Int32 m_CompletedState = c_StatePending;

        // Field that may or may not get set depending on usage
        private ManualResetEvent m_AsyncWaitHandle;

        // Fields set when operation completes
        private Exception m_exception;

        public AsyncResultNoResult(AsyncCallback asyncCallback, Object state)
        {
            m_AsyncCallback = asyncCallback;
            m_AsyncState = state;
        }

        public void SetAsCompleted(
           Exception exception, Boolean completedSynchronously)
        {
            // Passing null for exception means no error occurred. 
            // This is the common case
            m_exception = exception;

            // The m_CompletedState field MUST be set prior calling the callback
            Int32 prevState = Interlocked.Exchange(ref m_CompletedState,
               completedSynchronously ? c_StateCompletedSynchronously :
               c_StateCompletedAsynchronously);
            if (prevState != c_StatePending)
                throw new InvalidOperationException(
                    "You can set a result only once");

            // If the event exists, set it
            if (m_AsyncWaitHandle != null) m_AsyncWaitHandle.Set();

            // If a callback method was set, call it
            if (m_AsyncCallback != null) m_AsyncCallback(this);
        }

        public void EndInvoke()
        {
            // This method assumes that only 1 thread calls EndInvoke 
            // for this object
            if (!IsCompleted)
            {
                // If the operation isn't done, wait for it
                AsyncWaitHandle.WaitOne();
                AsyncWaitHandle.Close();
                m_AsyncWaitHandle = null;  // Allow early GC
            }

            // Operation is done: if an exception occured, throw it
            if (m_exception != null) throw m_exception;
        }

        #region Implementation of IAsyncResult
        public Object AsyncState { get { return m_AsyncState; } }

        public Boolean CompletedSynchronously
        {
            get
            {
                return Thread.VolatileRead(ref m_CompletedState) ==
                    c_StateCompletedSynchronously;
            }
        }

        public WaitHandle AsyncWaitHandle
        {
            get
            {
                if (m_AsyncWaitHandle == null)
                {
                    Boolean done = IsCompleted;
                    ManualResetEvent mre = new ManualResetEvent(done);
                    if (Interlocked.CompareExchange(ref m_AsyncWaitHandle,
                       mre, null) != null)
                    {
                        // Another thread created this object's event; dispose 
                        // the event we just created
                        mre.Close();
                    }
                    else
                    {
                        if (!done && IsCompleted)
                        {
                            // If the operation wasn't done when we created 
                            // the event but now it is done, set the event
                            m_AsyncWaitHandle.Set();
                        }
                    }
                }
                return m_AsyncWaitHandle;
            }
        }

        public Boolean IsCompleted
        {
            get
            {
                return Thread.VolatileRead(ref m_CompletedState) !=
                    c_StatePending;
            }
        }
        #endregion
    }

    internal class AsyncResult<TResult> : AsyncResultNoResult
    {
        // Field set when operation completes
        private TResult m_result = default(TResult);

        public AsyncResult(AsyncCallback asyncCallback, Object state) :
            base(asyncCallback, state) { }

        public void SetAsCompleted(TResult result,
           Boolean completedSynchronously)
        {
            // Save the asynchronous operation's result
            m_result = result;

            // Tell the base class that the operation completed 
            // sucessfully (no exception)
            base.SetAsCompleted(null, completedSynchronously);
        }

        new public TResult EndInvoke()
        {
            base.EndInvoke(); // Wait until operation has completed 
            return m_result;  // Return the result (if above didn't throw)
        }
    }
}
