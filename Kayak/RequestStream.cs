using System;
using System.IO;
using System.Linq;

namespace Kayak
{
    class AsyncResult : IAsyncResult
    {
        internal int BytesRead;
        internal Stream Stream;

        #region IAsyncResult Members

        public object AsyncState { get; internal set; }
        public bool CompletedSynchronously { get; internal set; }
        public bool IsCompleted { get; internal set; }

        public System.Threading.WaitHandle AsyncWaitHandle
        {
            get { return null; }
        }

        #endregion
    }

    public class RequestStream : Stream
    {
        ISocket socket;
        byte[] first;
        long length, position;
        int bytesCopied;

        IAsyncResult asyncResult;
        Stream reading;

        public RequestStream(ISocket socket, byte[] first, long length)
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

            if (position < first.Length)
            {
                bytesToRead = Math.Min(first.Length - position, bytesToRead);

                //Console.WriteLine("Copying " + bytesRead + " bytes from first.");
                Buffer.BlockCopy(first, (int)position, buffer, offset, (int)bytesToRead);
                position += bytesToRead;

                return new int[] { (int)bytesToRead }.ToObservable();
            }

            return socket.Read(buffer, offset, count);
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException("No synchronous reads, punk!");
        }

        //public override int Read(byte[] buffer, int offset, int count)
        //{
        //    long bytesToRead = Math.Min(length - position, (long)count);

        //    if (bytesToRead == 0) return 0;
        //    int bytesRead = 0;

        //    if (position < first.Length)
        //    {
        //        bytesRead = (int)Math.Min(first.Length - position, bytesToRead);

        //        //Console.WriteLine("Copying " + bytesRead + " bytes from first.");
        //        Buffer.BlockCopy(first, (int)position, buffer, offset, bytesRead);
        //        bytesToRead -= bytesRead;
        //    }

        //    if (bytesToRead > 0)
        //    {
        //        //Console.WriteLine("Reading " + bytesToRead + " bytes from underlying.");
        //        bytesRead += underlying.Read(buffer, offset + bytesRead, (int)bytesToRead);
        //    }

        //    position += bytesRead;
        //    //Console.WriteLine("Position is " + position);
        //    return bytesRead;
        //}

        //public override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
        //{
        //    long bytesToRead = Math.Min(length - position, (long)count);
        //    bytesCopied = 0;

        //    if (bytesToRead == 0)
        //    {
        //        asyncResult = new AsyncResult() { BytesRead = 0, CompletedSynchronously = true };
        //        callback(asyncResult);
        //        return asyncResult;
        //    }

        //    int bytesRead = 0;

        //    if (position < first.Length)
        //    {
        //        bytesCopied = bytesRead = (int)Math.Min(first.Length - position, bytesToRead);

        //        Trace.Write("Should copy {0} bytes from first.", bytesCopied);
        //        if (bytesRead > 0)
        //        {
        //            Trace.Write("Copying " + bytesRead + " bytes from first");
        //            Buffer.BlockCopy(first, (int)position, buffer, offset, bytesRead);
        //            bytesToRead -= bytesRead;

        //            asyncResult = new AsyncResult() { BytesRead = bytesRead, CompletedSynchronously = true };
        //            callback(asyncResult);
        //            return asyncResult;
        //        }
        //    }
        //    else
        //    {
        //        Trace.Write("Reading " + bytesToRead + " bytes from underlying.");
        //        asyncResult = underlying.BeginRead(buffer, offset + bytesRead, (int)bytesToRead, callback, state);
        //    }
  
        //    return asyncResult;
        //}

        //public override int EndRead(IAsyncResult iasr)
        //{   
        //    int bytesRead = bytesCopied;

        //    if (asyncResult is AsyncResult)
        //    {
        //        //assert bytesRead = 0;
        //        bytesRead = (asyncResult as AsyncResult).BytesRead;
        //        asyncResult = null;
        //    }
        //    else
        //    {
        //        Trace.Write("Underlying EndRead....");
        //        bytesRead = underlying.EndRead(iasr);
        //    }

        //    Trace.Write("EndRead bytesRead = " + bytesRead);

        //    position += bytesRead;

        //    Trace.Write("Position is " + position);
        //    return bytesRead;
        //}

        //public override int ReadByte()
        //{
        //    int result = 0;

        //    if (first.Position < first.Length)
        //    {
        //        result = first.ReadByte();
        //        if (result == -1)
        //            underlying.ReadByte();
        //    }
        //    else
        //        underlying.ReadByte();

        //    if (result != -1)
        //        position++;

        //    return result;
        //}

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
