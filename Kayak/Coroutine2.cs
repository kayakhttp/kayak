using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;

namespace Kayak
{
    public class Coroutine2<T>
    {
        IEnumerator<object> continuation;
        ProperAsyncResult<T> asyncResult;

        public Coroutine2(IEnumerator<object> continuation)
        {
            this.continuation = continuation;
        }

        public IAsyncResult BeginInvoke(AsyncCallback cb, object state)
        {
            asyncResult = new ProperAsyncResult<T>(cb, state);

            Continue(true);

            return asyncResult;
        }

        public T EndInvoke(IAsyncResult result)
        {
            Console.WriteLine("Coroutine.EndInvoke");
            ProperAsyncResult<T> ar = (ProperAsyncResult<T>)result;
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
                    Console.WriteLine("Will continuing after Task.");
                    task.ContinueWith(t => {
                        bool synch = ((IAsyncResult)t).CompletedSynchronously;
                        if (t.IsFaulted)
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
                    });
                    task.Start();
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
        public static Task<T> AsCoroutine2<T>(this IEnumerable<object> iteratorBlock)
        {
            return iteratorBlock.AsCoroutine2<T>(TaskCreationOptions.None);
        }

        public static Task<T> AsCoroutine2<T>(this IEnumerable<object> iteratorBlock, TaskCreationOptions opts)
        {
            var coroutine = new Coroutine2<T>(iteratorBlock.GetEnumerator());
            return Task.Factory.FromAsync<T>(
               (cb, s) => coroutine.BeginInvoke(cb, s),
               (iasr) => coroutine.EndInvoke(iasr), null, opts);
        }
    }

    // lifted from 
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

    internal class ProperAsyncResult<TResult> : AsyncResultNoResult
    {
        // Field set when operation completes
        private TResult m_result = default(TResult);

        public ProperAsyncResult(AsyncCallback asyncCallback, Object state) :
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
