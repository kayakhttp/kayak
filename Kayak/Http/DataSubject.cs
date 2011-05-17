using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Kayak.Http
{
    // a pass-through producer. if not connected, events are discarded.
    class DataSubject : IDataProducer, IDataConsumer
    {
        IDataConsumer channel;
        readonly Func<IDisposable> disposable;

        public DataSubject(Func<IDisposable> disposable)
        {
            this.disposable = disposable;
        }

        public IDisposable Connect(IDataConsumer channel)
        {
            this.channel = channel;
            return disposable();
        }

        public void OnError(Exception e)
        {
            if (channel == null)
                return;

            channel.OnError(e);
        }

        public bool OnData(ArraySegment<byte> data, Action continuation)
        {
            if (channel == null)
                return false;

            return channel.OnData(data, continuation);
        }

        public void OnEnd()
        {
            if (channel == null)
                return;

            channel.OnEnd();
        }
    }
}
