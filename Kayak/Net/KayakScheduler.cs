using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace Kayak
{
    public class KayakScheduler : TaskScheduler, IScheduler
    {
        public event EventHandler OnStarted;
        public event EventHandler OnStopped;

        Thread dispatch;
        ManualResetEventSlim wh;
        ConcurrentQueue<Task> queue;
        bool stopped;

        public void Post(Action action)
        {
            Debug.WriteLine("--- Posted task.");

            var task = new Task(action);
            task.ContinueWith(t =>
            {
                Debug.WriteLine("Error on scheduler.");
                t.Exception.PrintStacktrace();
            }, CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted, this);
            task.Start(this);
        }

        public KayakScheduler()
        {
            queue = new ConcurrentQueue<Task>();
        }

        public void Start()
        {
            if (dispatch != null)
                throw new InvalidOperationException("The scheduler was already started.");

            dispatch = new Thread(Dispatch);
            dispatch.Start();
        }

        public void Stop()
        {
            if (dispatch == null)
                throw new InvalidOperationException("The scheduler was not started.");

            Debug.WriteLine("Scheduler will stop.");
            Post(() => { stopped = true; });
        }

        void Dispatch()
        {
            wh = new ManualResetEventSlim();

            if (OnStarted != null)
                OnStarted(this, EventArgs.Empty);

            while (true)
            {
                Task outTask = null;

                if (queue.TryDequeue(out outTask))
                {
                    Debug.WriteLine("--- Executing Task ---");
                    TryExecuteTask(outTask);
                    Debug.WriteLine("--- Done Executing Task ---");

                    if (stopped)
                    {
                        stopped = false;
                        dispatch = null;
                        queue = new ConcurrentQueue<Task>();

                        Debug.WriteLine("Scheduler stopped.");
                        if (OnStopped != null)
                            OnStopped(this, EventArgs.Empty);

                        break;
                    }
                }
                else
                {
                    wh.Wait();
                    wh.Reset();
                }
            }

            stopped = false;
            dispatch = null;
            wh.Dispose();
            wh = null;
        }

        protected override IEnumerable<Task> GetScheduledTasks()
        {
            yield break;
        }

        public override int MaximumConcurrencyLevel { get { return 1; } }

        protected override void QueueTask(Task task)
        {
            queue.Enqueue(task);
            if (wh != null)
            {
                wh.Set();
            }
        }

        protected override bool TryDequeue(Task task)
        {
            Task outTask = null;
            queue.TryDequeue(out outTask);
            return (task == outTask);
        }

        protected override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued)
        {
            if (Thread.CurrentThread != dispatch) return false;

            if (taskWasPreviouslyQueued && !TryDequeue(task))
            {
                return false;
            }

            return TryExecuteTask(task);
        }
    }
}
