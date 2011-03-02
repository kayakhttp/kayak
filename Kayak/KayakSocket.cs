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
        public List<ArraySegment<byte>> Data;
        public int Size;

        public SocketBuffer()
        {
            Data = new List<ArraySegment<byte>>();
        }

        public void Add(ArraySegment<byte> data)
        {
            var d = new byte[data.Count];
            Buffer.BlockCopy(data.Array, data.Offset, d, 0, d.Length);

            lock (this)
            {
                Size += data.Count;
                Data.Add(new ArraySegment<byte>(d));
            }
        }

        public void Remove(int howmuch)
        {
            lock (this)
            {
                if (howmuch > Size) throw new ArgumentOutOfRangeException("howmuch > size");

                Size -= howmuch;

                int remaining = howmuch;

                while (remaining > 0)
                {
                    var first = Data[0];

                    int count = first.Count;
                    if (count <= remaining)
                    {
                        remaining -= count;
                        Data.RemoveAt(0);
                    }
                    else
                    {
                        Data[0] = new ArraySegment<byte>(first.Array, first.Offset + remaining, count - remaining);
                        remaining = 0;
                    }
                }
            }
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

        SocketBuffer buffer;

        byte[] inputBuffer;
        Socket socket;
        bool gotDel;
        bool closed;
        bool noDelay;
        Action continuation;
        KayakServer server;


        internal KayakSocket(Socket socket, KayakServer server)
        {
            this.socket = socket;
            this.server = server;
            buffer = new SocketBuffer();
        }

        public void SetNoDelay(bool noDelay)
        {
            //socket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.NoDelay, noDelay);
        }

        public bool Write(ArraySegment<byte> data, Action continuation)
        {
            if (closed) 
                throw new InvalidOperationException("Socket is closed."); 
            
            if (this.continuation != null) 
                throw new InvalidOperationException("Write was pending.");

            if (data.Count == 0) return false;


            this.continuation = continuation;

            // XXX copy! could optimize here?
            buffer.Add(data);

            if (buffer.Size > 0)
            {
                if (this.continuation != null)
                    return true;
                else
                    return false;
            }
            else
            {
                return DoWrite();
            }
        }

        bool DoWrite()
        {
            if (buffer.Size == 0)
            {
                return false;
            }

            try
            {
                var ar0 = socket.BeginSend(buffer.Data, SocketFlags.None, ar =>
                {
                    if (ar.CompletedSynchronously) return;

                    CompleteWrite(ar);

                    if (!DoWrite() && continuation != null)
                        continuation();

                }, null);

                if (ar0.CompletedSynchronously)
                {
                    CompleteWrite(ar0);

                    return DoWrite();
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
                buffer.Remove(written);
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
