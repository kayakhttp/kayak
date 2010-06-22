using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Threading;

namespace KayakTests
{
    // overrides MemoryStream's asynchronous Read/Write methods to be synchronous for testing purposes.
    // (default implementation (at least on MS) delegates the operation to the threadpool.)
    class SynchronousMemoryStream : MemoryStream
    {
        public SynchronousMemoryStream() : base() { }
        public SynchronousMemoryStream(byte[] buffer) : base(buffer) { }

        AsyncResult read, write;

        public override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
        {
            read = new AsyncResult() { AsyncState = state };
            read.bytesRead = Read(buffer, offset, count);
            callback(read);
            return read;
        }

        public override int EndRead(IAsyncResult asyncResult)
        {
            if (asyncResult != read)
                throw new Exception("Bogus asyncResult.");

            var bytesRead = read.bytesRead;
            return bytesRead;
        }

        public override IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
        {
            Console.WriteLine("SMS writing " + count + " bytes.");
            write = new AsyncResult() { AsyncState = state };
            Write(buffer, offset, count);
            callback(write);
            return write;
        }

        public override void EndWrite(IAsyncResult asyncResult)
        {
            if (asyncResult != write)
                throw new Exception("Bogus asyncResult.");
        }


        class AsyncResult : IAsyncResult
        {
            public int bytesRead;
            public object AsyncState
            {
                get; set;
            }

            public System.Threading.WaitHandle AsyncWaitHandle
            {
                get { return null; }
            }

            public bool CompletedSynchronously
            {
                get { return true; }
            }

            public bool IsCompleted
            {
                get { return true; }
            }
        }

    }
}
