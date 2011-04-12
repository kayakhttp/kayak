using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Kayak.Http
{
    interface IOutgoingMessage
    {
        event EventHandler Ended;

        bool IsLast { get; set; }

        void Attach(ISocket socket);

        bool Write(ArraySegment<byte> data, Action continuation);
        void End();
    }

    abstract class OutgoingMessage
    {
        public bool IsLast;

        protected bool wroteHeaders;

        protected bool ended; // XXX on buffer state?
        public IOutputStream Output;


        protected OutgoingMessage(IOutputStream output)
        {
            Output = output;
        }

        protected bool WriteBody(ArraySegment<byte> data, Action continuation)
        {
            if (ended)
                throw new InvalidOperationException("Response was ended.");

            if (!wroteHeaders)
            {
                wroteHeaders = true;

                // want to make sure these go out in same packet
                // XXX can we do this better?

                var head = GetHead();
                var headPlusBody = new byte[head.Length + data.Count];
                System.Buffer.BlockCopy(head, 0, headPlusBody, 0, head.Length);
                System.Buffer.BlockCopy(data.Array, data.Offset, headPlusBody, head.Length, data.Count);

                return Write(new ArraySegment<byte>(headPlusBody), continuation);
            }
            else
                return Write(data, continuation);
        }

        protected bool Write(ArraySegment<byte> data, Action continuation)
        {
            return Output.Write(data, continuation);
        }

        protected abstract byte[] GetHead();

        public void End()
        {
            if (ended)
                throw new InvalidOperationException("Response was ended.");

            ended = true;

            if (!wroteHeaders)
            {
                wroteHeaders = true;
                var head = GetHead();
                Write(new ArraySegment<byte>(head), null);
            }

            Output.End();
        }
    }
}
