using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Kayak
{
    class Disposable : IDisposable
    {
        readonly Action dispose;

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
