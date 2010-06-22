using System;
using System.IO;

namespace Kayak
{
    /// <summary>
    /// Limits the number of bytes that can be written/read to/from an underlying Stream. 
    /// Has mutually exclusive reading and writing modes. Supports reading or writing
    /// a stream "first". In read mode, the "first "stream is read from (until its end) before 
    /// the underlying stream is read from, and bytes read from the first stream
    /// count toward the length of this stream. In writing mode, the "first"
    /// stream is written before any user data is written, and it's length does not
    /// count towards the length of this stream.
    /// </summary>
    class EntityStream : Stream
    {
        Stream underlying;
        Stream readFirst;
        long length;
        long position;
        bool reading;

        public EntityStream(Stream underlying, long length, FileMode mode, Stream first)
        {
            this.underlying = underlying;
            this.length = length;
            this.readFirst = readFirst;
            reading = true;
        }

        public EntityStream(Stream underlying, long length)
        {
            this.underlying = underlying;
            this.length = length;
            reading = false;
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            //Console.WriteLine("EntityStream.Read");
            //Console.WriteLine("offset = " + offset);
            //Console.WriteLine("count = " + count);

            if (!reading) throw new InvalidOperationException();

            long bytesToRead = Math.Min(length - position, (long)count);

            //Console.WriteLine("bytesToRead = " + bytesToRead);

            if (bytesToRead == 0) return 0;

            int bytesRead = 0;

            if (readFirst.Position < readFirst.Length)
            {
                //Console.WriteLine("reading first");
                bytesRead += readFirst.Read(buffer, offset, count);
                //Console.WriteLine("bytesRead = " + bytesRead);
                bytesToRead -= bytesRead;
            }
            //Console.WriteLine("bytesToRead = " + bytesToRead);

            if (bytesToRead > 0)
            {
                //Console.WriteLine("buffer.length " + buffer.Length);
                //Console.WriteLine("offset + bytesRead " + (offset + bytesRead));
                //Console.WriteLine("(int)bytesToRead - bytesRead" + ((int)bytesToRead - bytesRead));
                bytesRead += underlying.Read(buffer, offset + bytesRead, (int)bytesToRead);
            }

            position += bytesRead;
            return bytesRead;
        }

        public override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
        {
            if (!reading) throw new InvalidOperationException();

            long bytesToRead = Math.Min(length - position, (long)count);
            int bytesRead = 0;

            if (readFirst.Position < readFirst.Length)
            {
                bytesRead += readFirst.Read(buffer, offset + bytesRead, count);
                bytesToRead -= bytesRead;
                position += bytesRead;
            }

            return underlying.BeginRead(buffer, offset, count - bytesRead, callback, state);
        }

        public override int EndRead(IAsyncResult asyncResult)
        {
            int bytesRead = underlying.EndRead(asyncResult);

            position += bytesRead;

            return bytesRead;
        }

        public override int ReadByte()
        {
            int result = 0;

            if (readFirst.Position < readFirst.Length)
            {
                result = readFirst.ReadByte();
                if (result == -1)
                    underlying.ReadByte();
            }
            else
                underlying.ReadByte();

            if (result != -1)
                position++;

            return result;
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            if (reading) throw new InvalidOperationException();

            int bytesToWrite = (int)Math.Min(length - position, count);

            if (bytesToWrite == 0)
                return;

            position += bytesToWrite;

            underlying.Write(buffer, offset, bytesToWrite);
        }

        public override IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
        {
            int bytesToWrite = (int)Math.Min(length - position, count);

            position += bytesToWrite;

            return underlying.BeginWrite(buffer, offset, bytesToWrite, callback, state);
        }

        public override void EndWrite(IAsyncResult asyncResult)
        {
            base.EndWrite(asyncResult);
        }
        public override void WriteByte(byte value)
        {
            base.WriteByte(value);
            position++;
        }

        public override long Position
        {
            get { return position; }
            set { throw new NotSupportedException(); }
        }

        public override void Flush() { underlying.Flush(); }
        public override void Close() { underlying.Close(); }
        public override long Length { get { return length; } }
        public override bool CanRead { get { return reading && length - position > 0; } }
        public override bool CanWrite { get { return !reading && length - position > 0; } }
        public override bool CanSeek { get { return false; } }
        public override long Seek(long offset, SeekOrigin origin) { throw new NotSupportedException(); }
        public override void SetLength(long value) { throw new NotSupportedException(); }
    }
}
