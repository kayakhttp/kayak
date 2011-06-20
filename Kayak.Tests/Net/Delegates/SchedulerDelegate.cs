using System;
using Kayak;

namespace Kayak.Tests.Net
{
    class SchedulerDelegate : ISchedulerDelegate
    {
        public bool GotOnStopped;
        public Exception Exception;

        public Action OnStoppedAction = null;
        public Action<Exception> OnExceptionAction = null;

        public void OnStop(IScheduler scheduler)
        {
            if (GotOnStopped)
                throw new Exception("Already got OnStop");

            GotOnStopped = true;

            if (OnStoppedAction != null)
                OnStoppedAction();
        }

        public void OnException(IScheduler scheduler, Exception e)
        {
            Exception = e;
            if (OnExceptionAction != null)
                OnExceptionAction(e);
        }
    }
}
