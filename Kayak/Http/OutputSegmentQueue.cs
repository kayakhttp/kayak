using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Kayak.Http
{
    class OutputSegmentQueue : IObserver<IDataProducer>
    {
        ISocket socket;
        IOutputSegment tail;

        public OutputSegmentQueue(ISocket socket)
        {
            this.socket = socket;
        }

        // each one of these represents an entire response message (continue, headers, body)
        public void OnNext(IDataProducer message)
        {
            var c = new MessageConsumer(tail, message);

            if (tail == null)
                c.AttachSocket(socket);
            else
                tail.AttachNextSegment(c);

            tail = c;
        }

        // called if error on socket or whilst parsing, or if user code cancels request input
        public void OnError(Exception e)
        {
            if (tail == null)
                socket.Dispose();
            else
                // dispose all pending responses, last of which should dispose the socket
                tail.Dispose();
        }

        // called after the last message has been queued.
        // could be if !shouldKeepAlive on request - last message will have been queued
        // could be if user indicates connection close - could have pending messages, all should be cancelled
        public void OnCompleted()
        {
            var c = new LastConsumer(tail);

            if (tail == null)
            {
                // implicitly end and dispose socket
                c.AttachSocket(socket);
            }
            else
            {
                tail.AttachNextSegment(c);
                tail = c;
            }
        }
    }
}
