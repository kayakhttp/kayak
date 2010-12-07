using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Owin;

namespace Kayak
{
    class AsyncResult<T> : IAsyncResult
    {
        protected T result;
        
        public AsyncResult() { }
        public AsyncResult(T result)
        {
            this.result = result;
        }

        public T GetResult()
        {
            return result;
        }

        #region IAsyncResult Members

        public object AsyncState
        {
            get { throw new NotImplementedException(); }
        }

        public System.Threading.WaitHandle AsyncWaitHandle
        {
            get { throw new NotImplementedException(); }
        }

        public bool CompletedSynchronously
        {
            get { throw new NotImplementedException(); }
        }

        public bool IsCompleted
        {
            get { throw new NotImplementedException(); }
        }

        #endregion
    }
    class ObservableAsyncResult<T> : AsyncResult<T>
    {
        Exception e;
        IDisposable sx;

        public ObservableAsyncResult(IObservable<T> obs, AsyncCallback cb)
        {
            sx = obs.Subscribe(t => result = t, ex => { e = ex; cb(this); }, () => cb(this));
        }

        public T GetResult()
        {
            if (sx != null)
                sx.Dispose();
            if (e != null) throw new Exception("Observable contained exception.", e);
            return result;
        }
    }
}
