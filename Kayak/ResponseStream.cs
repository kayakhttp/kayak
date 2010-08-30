using System;
using System.Linq;
using System.IO;
using System.Collections.Generic;

namespace Kayak
{
    public class ResponseStream : Stream
    {
        ISocket socket;
        //Stream underlying;
        byte[] first;
        long length, position;

        IAsyncResult asyncResult;

        public ResponseStream(ISocket socket, byte[] first, long length)
        {
            this.socket = socket;
            this.first = first;
            this.length = length;
        }

        public IObservable<Unit> WriteAsync(byte[] buffer, int offset, int count)
        {
            if (first != null)
            {
                //Console.WriteLine("Writing first.");
                var combined = new byte[first.Length + buffer.Length];
                Buffer.BlockCopy(first, 0, combined, 0, first.Length);
                Buffer.BlockCopy(buffer, offset, combined, first.Length, count);
                first = null;

                return WriteAsync(combined, 0, combined.Length);
            }
            else
                return WriteInternal(buffer, offset, count).AsCoroutine<Unit>();
        }

        IEnumerable<object> WriteInternal(byte[] buffer, int offset, int count)
        {
            int bytesWritten = 0;

            while (bytesWritten < count)
                yield return socket.Write(buffer, offset + bytesWritten, count - bytesWritten).Do(n => bytesWritten += n);
        }

        public IObservable<int> WriteFileAsync(string file, int offset, int count)
        {
            return WriteFileAsyncInternal(file, offset, count).AsCoroutine<int>();
        }

        IEnumerable<object> WriteFileAsyncInternal(string file, int offset, int count)
        {
            if (first != null)
            {
                var ferst = first;
                first = null;
                yield return WriteAsync(ferst, 0, ferst.Length);
            }

            yield return socket.WriteFile(file, offset, count).Do(n => position += n);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException("No synchronous writes, bro!");
        }

        //public override void Write(byte[] buffer, int offset, int count)
        //{
        //    if (first != null)
        //    {
        //        //Console.WriteLine("Writing first.");
        //        //Console.WriteLine("underlying = null ? " + (underlying == null));
        //        underlying.Write(first, 0, first.Length);
        //        //Console.WriteLine("asdf");
        //        position += first.Length;
        //        first = null;
        //    }

        //    //Console.WriteLine("Writing " + count + " bytes");
        //    underlying.Write(buffer, offset, count);
        //    position += count;
        //}

        //public override IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
        //{
        //    if (first != null)
        //    {
        //        //Console.WriteLine("Writing first.");
        //        var combined = new byte[first.Length + buffer.Length];
        //        Buffer.BlockCopy(first, 0, combined, 0, first.Length);
        //        Buffer.BlockCopy(buffer, offset, combined, first.Length, count);
        //        first = null;
        //        asyncResult = underlying.BeginWrite(combined, 0, combined.Length, callback, state);
        //        position += combined.Length;
        //    }
        //    else
        //    {
        //        asyncResult = underlying.BeginWrite(buffer, offset, count, callback, state);
        //        position += count;
        //    }


        //    return asyncResult;
        //}

        //public override void EndWrite(IAsyncResult asyncResult)
        //{
        //    //Console.WriteLine("EndWrite.");
        //    underlying.EndWrite(asyncResult);
        //}

        public override bool CanRead
        {
            get { return false; }
        }

        public override bool CanSeek
        {
            get { return false; }
        }

        public override bool CanWrite
        {
            get { return true; }
        }

        public override void Flush()
        {
            //underlying.Flush();
        }

        public override long Length
        {
            get { return length; }
        }

        public override long Position
        {
            get
            {
                return position;
            }
            set
            {
                throw new InvalidOperationException();
            }
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            throw new InvalidOperationException();
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new InvalidOperationException();
        }

        public override void SetLength(long value)
        {
            throw new InvalidOperationException();
        }
    }
}
