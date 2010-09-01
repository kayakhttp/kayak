using System;
using System.IO;
using System.Linq;

namespace Kayak
{
    public class RequestStream : Stream
    {
        ISocket socket;
        ArraySegment<byte> first;
        int firstOffset, firstLength;
        long length, position;

        // TODO consider the possibility that length is -1 (unknown)
        public RequestStream(ISocket socket, ArraySegment<byte> first, long length)
        {
            this.socket = socket;
            this.first = first;
            this.length = length;
        }

        public IObservable<int> ReadAsync(byte[] buffer, int offset, int count)
        {
            long bytesToRead = Math.Min(length - position, (long)count);

            if (bytesToRead == 0)
                return new int[] { 0 }.ToObservable();

            if (position < first.Count)
            {
                bytesToRead = Math.Min(first.Count - position, bytesToRead);

                //Console.WriteLine("Copying " + bytesRead + " bytes from first.");
                Buffer.BlockCopy(first.Array, first.Offset + (int)position, buffer, offset, (int)bytesToRead);
                position += bytesToRead;

                return new int[] { (int)bytesToRead }.ToObservable();
            }

            return socket.Read(buffer, offset, count);
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException("No synchronous reads, punk!");
        }

        public override bool CanRead
        {
            get { return true; }
        }

        public override bool CanSeek
        {
            get { return false; }
        }

        public override bool CanWrite
        {
            get { return false; }
        }

        public override void Flush()
        {
            throw new NotSupportedException();
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
                throw new NotSupportedException();
            }
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }
    }
}
