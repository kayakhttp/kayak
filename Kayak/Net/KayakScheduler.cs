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
    public class KayakScheduler : IScheduler
    {
        public event EventHandler OnStarted;
        public event EventHandler OnStopped;

        volatile int running;

        public void Start()
        {
            if (Interlocked.CompareExchange(ref running, 1, 0) == 1)
                throw new InvalidOperationException("The scheduler was already started.");

            if (OnStarted != null)
                OnStarted(this, EventArgs.Empty);
        }

        public void Stop()
        {
            if (Interlocked.CompareExchange(ref running, 0, 1) == 0)
                throw new InvalidOperationException("The scheduler was not started.");

            if (OnStopped != null)
                OnStopped(this, EventArgs.Empty);
        }

        public void Post(Action action)
        {
            if (running == 0)
                return;

            Task.Factory
                .StartNew(action)
                .ContinueWith(t => { 
                    Debug.WriteLine("Error on scheduler.");
                    t.Exception.PrintStacktrace();
                }, TaskContinuationOptions.OnlyOnFaulted);
        }
    }
}
