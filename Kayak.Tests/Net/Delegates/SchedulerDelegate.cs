using System;
using Kayak;

namespace Kayak.Tests.Net
{
    class SchedulerDelegate : ISchedulerDelegate
    {
        public Exception Exception;

        public Action OnStartedAction;
        public Action OnStoppedAction;
        public Action<Exception> OnExceptionAction;

        public void OnStop(IScheduler scheduler)
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
            Exception = e;
            if (OnExceptionAction != null)
                OnExceptionAction(e);
        }
    }
}
