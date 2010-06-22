using System;
using System.Collections.Generic;
using System.Text;

namespace Kayak
{   
    class ObserverCollection<T>
    {
        List<IObserver<T>> observers;

        public ObserverCollection()
        {
            observers = new List<IObserver<T>>(1);
        }

        public void Error(Exception e)
        {
            observers.ForEach(o => o.OnError(e));
        }

        public void Completed()
        {
            observers.ForEach(o => o.OnCompleted());
        }

        public void Next(T v)
        {
            observers.ForEach(o => o.OnNext(v));
        }

        public IDisposable Add(IObserver<T> observer)
        {
            lock (observers)
                observers.Add(observer);
            return new ObserverRemover<T>(this, observer);
        }

        void Remove(IObserver<T> observer)
        {
            lock (observers)
                observers.Remove(observer);
        }

        // this too...
        class ObserverRemover<T> : IDisposable
        {
            ObserverCollection<T> c;
            IObserver<T> o;

            public ObserverRemover(ObserverCollection<T> c, IObserver<T> o)
            {
                this.c = c;
                this.o = o;
            }

            public void Dispose()
            {
                c.Remove(o);
            }
        }
    }
}
