using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Kayak.Http
{
    class DataSubject : IDataProducer, IDataConsumer
    {
        IDataConsumer channel;
        readonly Func<IDisposable> disposable;

        bool gotEnd;
        Exception error;
        DataBuffer buffer;

        public DataSubject(Func<IDisposable> disposable)
        {
            this.disposable = disposable;
        }

        public IDisposable Connect(IDataConsumer channel)
        {
            this.channel = channel;

            if (buffer != null)
            {
                buffer.Each(d => channel.OnData(new ArraySegment<byte>(d), null));

                // XXX this maybe is kinda wrong.
                if (continuation != null)
                    continuation();
            }

            if (error != null)
                channel.OnError(error);

            if (gotEnd)
                channel.OnEnd();

            return disposable();
        }

        public void OnError(Exception e)
        {
            if (channel == null)
                error = e;
            else
                channel.OnError(e);
        }

        Action continuation;

        public bool OnData(ArraySegment<byte> data, Action ack)
        {
            if (channel == null)
            {
                if (buffer == null)
                    buffer = new DataBuffer();

                buffer.Add(data);

                if (ack != null)
                {
                    this.continuation = ack;
                    return true;
                }

                return false;
            }
            else
                return channel.OnData(data, ack);
        }

        public void OnEnd()
        {
            if (channel == null)
                gotEnd = true;
            else
                channel.OnEnd();
        }
    }

    class DataBuffer
    {
        List<byte[]> buffer = new List<byte[]>();

        public string GetString()
        {
            return buffer.Aggregate("", (acc, next) => acc + Encoding.UTF8.GetString(next));
        }

        public int GetCount()
        {
            return buffer.Aggregate(0, (c, d) => c + d.Length);
        }

        public void Add(ArraySegment<byte> d)
        {
            // XXX maybe we should have our own allocator? i don't know. maybe the runtime is good
            // about this in awesome ways.
            byte[] b = new byte[d.Count];
            System.Buffer.BlockCopy(d.Array, d.Offset, b, 0, d.Count);
            buffer.Add(b);
        }

        public void Each(Action<byte[]> each)
        {
            buffer.ForEach(each);
        }
    }
}
