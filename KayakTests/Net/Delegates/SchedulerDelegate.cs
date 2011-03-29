using System;
using Kayak;

namespace KayakTests.Net
{
    class SchedulerDelegate : IDisposable
    {
        IScheduler scheduler;

        public Action OnStarted;
        public Action OnStopped;

        public SchedulerDelegate(IScheduler scheduler)
        {
            this.scheduler = scheduler;
            scheduler.OnStarted += new EventHandler(scheduler_OnStarted);
            scheduler.OnStopped += new EventHandler(scheduler_OnStopped);
        }

        public void Dispose()
        {
            scheduler.OnStarted -= new EventHandler(scheduler_OnStarted);
            scheduler.OnStopped -= new EventHandler(scheduler_OnStopped);
            this.scheduler = null;
        }

        void scheduler_OnStopped(object sender, EventArgs e)
        {
            if (OnStopped != null)
                OnStopped();
        }

        void scheduler_OnStarted(object sender, EventArgs e)
        {
            if (OnStarted != null)
                OnStarted();
        }
    }
}
