using System;
using System.Linq;
using System.IO;
using System.Collections.Generic;

namespace Kayak
{
    public class ResponseStream : Stream
    {
        ISocket socket;
        byte[] first;
        long length, position;

        public ResponseStream(ISocket socket, byte[] first, long length)
        {
            this.socket = socket;
            this.first = first;
            this.length = length;
        }

        public IObservable<Unit> WriteAsync(byte[] buffer)
        {
            return WriteAsync(buffer, 0, buffer.Length);
        }

        public IObservable<Unit> WriteAsync(ArraySegment<byte> buffer)
        {
            // TODO implement all other write overloads in terms of this one.
            return WriteAsync(buffer.Array, buffer.Offset, buffer.Count);
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
