using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.Collections.Concurrent;

namespace Kayak
{
    public class KayakScheduler : TaskScheduler, IDisposable
    {
        Thread dispatch;
        ManualResetEventSlim wh;
        ConcurrentQueue<Task> queue;
        bool stopped;

        public void Post(Action action)
        {
            Task.Factory.StartNew(action, CancellationToken.None, TaskCreationOptions.None, this);
        }

        public KayakScheduler()
        {
            wh = new ManualResetEventSlim();
            queue = new ConcurrentQueue<Task>();
        }

        public void Dispose()
        {
            wh.Dispose();
        }

        public void Start()
        {
            if (dispatch != null)
                throw new InvalidOperationException();

            dispatch = new Thread(Dispatch);
            dispatch.Start();
        }

        void Stop()
        {
            Post(() => stopped = true);
        }

        void Dispatch()
        {

            while (true)
            {
                Task outTask = null;

                if (queue.TryDequeue(out outTask))
                {
                    TryExecuteTask(outTask);
                    if (stopped) break;
                }
                else
                {
                    wh.Wait();
                    wh.Reset();
                }
            }

            stopped = false;
            dispatch = null;
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
            wh.Set();
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
                return false;

            return TryExecuteTask(task);
        }
    }
}
