using System;
using Kayak;

namespace KayakTests.Net
{
    class SchedulerDelegate : ISchedulerDelegate
    {
        public Action OnStartedAction;
        public Action OnStoppedAction;
        public Action<Exception> OnExceptionAction;

        public void OnStopped(IScheduler scheduler)
        {
            if (OnStoppedAction != null)
                OnStoppedAction();
        }

        public void OnStarted(IScheduler scheduler)
        {
            if (OnStartedAction != null)
                OnStartedAction();
        }

        public void OnException(IScheduler scheduler, Exception e)
        {
            if (OnExceptionAction != null)
                OnExceptionAction(e);
        }
    }
}
