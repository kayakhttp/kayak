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

            Size += data.Count;
            Data.Add(new ArraySegment<byte>(d));
        }

        public void Remove(int howmuch)
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
        bool ended;
        bool aborted;
        Action continuation;
        KayakServer server;


        internal KayakSocket(Socket socket, KayakServer server)
        {
            socket.SendTimeout = socket.ReceiveTimeout = 1000;
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
            if (ended)
                throw new InvalidOperationException("The socket was ended.");

            if (closed) 
                throw new InvalidOperationException("The socket was closed."); 
            
            if (this.continuation != null) 
                throw new InvalidOperationException("Write was pending.");

            if (data.Count == 0) return false;


            this.continuation = continuation;
            var size = buffer.Size;

            // XXX copy! could optimize here?
            buffer.Add(data);

            if (size > 0)
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
            if (buffer.Size == 0 || closed || aborted)
            {
                Console.WriteLine("Writing finished.");
                return false;
            }

            try
            {
                var ar0 = socket.BeginSend(buffer.Data, SocketFlags.None, ar =>
                {
                    server.Scheduler.Post(() =>
                    {
                        if (ar.CompletedSynchronously) return;

                        CompleteWrite(ar);

                        if (!DoWrite() && continuation != null)
                            continuation();
                    });
                }, null);

                if (ar0.CompletedSynchronously)
                {
                    CompleteWrite(ar0);

                    return DoWrite();
                }

                return true;
            }
            catch (SocketException e)
            {
                HandleSocketException(e);
                return false;
            }
            catch (Exception e)
            {
                Delegate.OnError(this, new Exception("Exception on write. ", e));
                return false;
            }
        }

        void CompleteWrite(IAsyncResult ar)
        {
            try
            {
                if (closed) return;

                var written = socket.EndSend(ar);
                buffer.Remove(written);

                Debug.WriteLine("Wrote " + written + " " + (ar.CompletedSynchronously ? "" : "a") + "sync, buffer size is " + buffer.Size);

                if (ended)
                    ShutdownIfBufferIsEmpty();
            }
            catch (Exception e)
            {
                del.OnError(this, new Exception("Exception during write callback.", e));
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
                    server.Scheduler.Post(() =>
                    {
                        if (closed)
                        {
                            Debug.WriteLine("Got Receive callback after object disposed.");
                            return;
                        }
                        int read = 0;
                        try
                        {
                            read = socket.EndReceive(ar);
                            Debug.WriteLine("Read " + read);
                        }
                        catch (Exception e)
                        {
                            del.OnError(this, new Exception("Error while reading.", e));
                            return;
                        }

                        if (read == 0)
                        {
                            del.OnEnd(this);
                        }
                        else
                        {
                            if (!del.OnData(this, new ArraySegment<byte>(inputBuffer, 0, read), DoRead))
                                DoRead();
                        }
                    });
                }, null);
            }
            catch (SocketException e)
            {
                HandleSocketException(e);
            }
            catch (Exception e)
            {
                del.OnError(this, new Exception("Error while reading.", e));
            }
        }

        public void End()
        {
            if (closed)
                throw new InvalidOperationException("The socket was closed.");
            if (ended) return;

            ended = true;
            ShutdownIfBufferIsEmpty();
        }

        void HandleSocketException(SocketException e)
        {
            if (e.ErrorCode == 10053 || e.ErrorCode == 10054)
            {
                aborted = true;
                Debug.WriteLine("Connection aborted/reset.");
                del.OnEnd(this);
                return;
            }

            del.OnError(this, new Exception("SocketException while reading.", e));
        }

        void ShutdownIfBufferIsEmpty()
        {
            if (buffer.Size == 0)
            {
                socket.Shutdown(SocketShutdown.Send);
                Debug.WriteLine("Shut down socket outgoing.");
            }
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
