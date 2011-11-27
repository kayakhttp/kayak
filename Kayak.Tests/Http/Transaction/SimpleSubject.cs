using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Kayak.Tests.Http
{
    class SimpleSubject : IDataConsumer, IDataProducer
    {
        IDataConsumer consumer;
        Action disconnect;
        Action connect;

        public SimpleSubject(Action connect, Action disconnect)
        {
            this.connect = connect;
            this.disconnect = disconnect;
        }

        public void OnError(Exception e)
        {
            consumer.OnError(e);
        }

        public bool OnData(ArraySegment<byte> data, Action continuation)
        {
            return consumer.OnData(data, continuation);
        }

        public void OnEnd()
        {
            consumer.OnEnd();
        }

        public IDisposable Connect(IDataConsumer channel)
        {
            this.consumer = channel;
            connect();
            return new Disposable(disconnect);
        }
    }
}
