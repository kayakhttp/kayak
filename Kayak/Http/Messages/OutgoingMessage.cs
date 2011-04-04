using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Kayak.Http
{
    abstract class OutgoingMessage
    {
        public bool IsLast;

        protected bool wroteHeaders;

        protected bool ended; // XXX on buffer state?
        public SocketBuffer Buffer;


        protected OutgoingMessage(SocketBuffer buffer)
        {
            this.Buffer = buffer;
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

                return Send(new ArraySegment<byte>(headPlusBody), continuation);
            }
            else
                return Send(data, continuation);
        }

        protected bool Send(ArraySegment<byte> data, Action continuation)
        {
            return Buffer.Write(data, continuation);
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
                Send(new ArraySegment<byte>(head), null);
            }

            Buffer.End();
        }
    }
}
