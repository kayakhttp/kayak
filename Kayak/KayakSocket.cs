using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Diagnostics;

namespace Kayak
{
    class SocketBuffer
    {
        LinkedList<byte[]> buf;
        public int Size;

        public SocketBuffer()
        {
            buf = new LinkedList<byte[]>();
        }

        public void Add(ArraySegment<byte> data)
        {
            Size += data.Count;
            var d = new byte[data.Count];
            Buffer.BlockCopy(data.Array, data.Offset, d, 0, d.Length);
            buf.AddLast(d);
        }

        public byte[] Remove()
        {
            if (buf.Count == 0)
                return null;

            var b = buf.First.Value;
            buf.RemoveFirst();
            Size -= b.Length;
            return b;
        }
    }

    class KayakSocket : ISocket
    {
        ISocketDelegate del;
        
        public ISocketDelegate Delegate
        {
            get { return del; }
            set
            {
                del = value;
                if (!gotDel)
                {
                    gotDel = true;
                    DoRead();
                }
            }
        }

        public IPEndPoint RemoteEndPoint { get; private set; }

        SocketBuffer buf;
        ArraySegment<byte> sending;

        byte[] inputBuffer;
        Socket socket;
        bool gotDel;
        bool closed;
        bool noDelay;
        KayakServer server;
        Action continuation;

        internal KayakSocket(Socket socket, KayakServer server)
        {
            this.socket = socket;
            this.server = server;
            buf = new SocketBuffer();
        }

        public void SetNoDelay(bool noDelay)
        {
            //socket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.NoDelay, noDelay);
        }

        public bool Write(ArraySegment<byte> data, Action continuation)
        {
            if (continuation == null)
                throw new NotSupportedException("Buffered writes are not supported right now.");

            if (closed) 
                throw new InvalidOperationException("Socket is closed."); 
            
            if (this.continuation != null) 
                throw new InvalidOperationException("Write was pending.");

            if (data.Count == 0) return false;

            sending = data;

            return DoWrite(continuation);
        }

        bool DoWrite(Action continuation)
        {
            try
            {
                var ar0 = socket.BeginSend(sending.Array, sending.Offset, sending.Count, SocketFlags.None, ar =>
                {
                    if (ar.CompletedSynchronously) return;

                    CompleteWrite(ar);

                    if (sending.Count > 0)
                    {
                        if (!DoWrite(continuation))
                            continuation();
                    }
                    else
                        continuation();

                }, null);

                if (ar0.CompletedSynchronously)
                {
                    CompleteWrite(ar0);

                    if (sending.Count > 0)
                    {
                        return DoWrite(continuation);
                    }
                    else
                        return false;
                }

                return true;
            }
            catch (Exception e)
            {
                Delegate.OnError(this, new Exception("Exception on write. ", e));
                Dispose();
                return false;
            }
        }

        void CompleteWrite(IAsyncResult ar)
        {
            try
            {
                var written = socket.EndSend(ar);
                Debug.WriteLine("Wrote " + written);

                sending = new ArraySegment<byte>(sending.Array, sending.Offset + written, sending.Count - written);
            }
            catch (Exception e)
            {
                Delegate.OnError(this, new Exception("Exception during write callback.", e));
                Dispose();
            }
        }

        void DoRead()
        {
            if (inputBuffer == null)
                inputBuffer = new byte[1024 * 4];

            try
            {
                Debug.WriteLine("Reading.");
                socket.BeginReceive(inputBuffer, 0, inputBuffer.Length, SocketFlags.None, ar =>
                {
                    try
                    {
                        var read = socket.EndReceive(ar);
                        Debug.WriteLine("Read " + read);
                        if (read == 0)
                            del.OnEnd(this);
                        else
                        {
                            if (!del.OnData(this, new ArraySegment<byte>(inputBuffer, 0, read), DoRead))
                                DoRead();
                        }
                    }
                    catch (Exception e)
                    {
                        del.OnError(this, new Exception("Error while reading.", e));
                        Dispose();
                    }
                }, null);
            }
            catch (Exception e)
            {
                del.OnError(this, new Exception("Error while reading.", e));
                Dispose();
            }
        }

        public void End()
        {
            closed = true;
            socket.Shutdown(SocketShutdown.Send);
        }

        public void Dispose()
        {
            if (closed) return;

            Debug.WriteLine("Closing socket.");
            closed = true;
            socket.Close();
            server.SocketClosed();
            server = null;
        }
    }
}
