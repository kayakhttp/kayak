using System;

namespace Oars
{
    class Disposable : IDisposable
    {
        readonly Action dispose;

        public Disposable() : this(() => { }) { }

        public Disposable(Action dispose)
        {
            this.dispose = dispose;
        }

        public void Dispose()
        {
            dispose();
        }
    }
}
