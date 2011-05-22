using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Kayak;
using System.Threading;
using System.Diagnostics;

namespace KayakTests.Net
{
    class EventContext : IDisposable
    {
        IScheduler scheduler;
        ManualResetEventSlim wh;
        public Action OnStarted;

        public EventContext(IScheduler scheduler)
        {
            this.scheduler = scheduler;
            wh = new ManualResetEventSlim(false);
        }

        void scheduler_OnStarted(object sender, EventArgs e)
        {
            if (OnStarted != null)
                OnStarted();
        }

        void scheduler_OnStopped(object sender, EventArgs e)
        {
            wh.Set();
        }

        public void Run()
        {
            scheduler.Start();
            wh.Wait(TimeSpan.FromSeconds(1));
            Debug.WriteLine("EventContext: Done waiting.");
        }

        public void Dispose()
        {
            //scheduler.OnStopped -= new EventHandler(scheduler_OnStopped);
        }
    }
}
