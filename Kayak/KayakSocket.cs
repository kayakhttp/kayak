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
        public event EventHandler OnConnected;
        public event EventHandler<DataEventArgs> OnData;
        public event EventHandler OnEnd;
        public event EventHandler OnTimeout;
        public event EventHandler<ExceptionEventArgs> OnError;
        public event EventHandler OnClose;

        [ThreadStatic]
        static readonly ExceptionEventArgs ExceptionEventArgs = new ExceptionEventArgs();

        [ThreadStatic]
        static readonly DataEventArgs DataEventArgs = new DataEventArgs();

        public int id;
        static int nextId;

        public IPEndPoint RemoteEndPoint { get; private set; }

        SocketBuffer buffer;

        byte[] inputBuffer;
        Socket socket;
        bool gotDel;
        bool disposed;
        bool writeEnded, readEnded, closed;
        Action continuation;
        KayakServer server;

        internal KayakSocket(Socket socket, KayakServer server)
        {
            this.id = nextId++;
            this.socket = socket;
            this.server = server;
            buffer = new SocketBuffer();
        }

        public void Connect(IPEndPoint ep)
        {
            this.socket = new Socket(new SocketInformation() { Options = SocketInformationOptions.UseOnlyOverlappedIO });
            socket.BeginConnect(ep, iasr => {
                KayakScheduler.Current.Post(() => { 
                    if (OnConnected != null)
                        OnConnected(this, EventArgs.Empty); 
                });
            }, null);
        }

        public void SetNoDelay(bool noDelay)
        {
            //socket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.NoDelay, noDelay);
        }

        public bool Write(ArraySegment<byte> data, Action continuation)
        {
            if (disposed)
                throw new ObjectDisposedException("socket");

            if (writeEnded)
                throw new InvalidOperationException("The socket was previously ended.");
            
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
                var result = DoWrite();

                if (!result)
                {
                    continuation = null;
                }

                return result;
            }
        }

        bool DoWrite()
        {
            if (buffer.Size == 0 || disposed)
            {
                Debug.WriteLine("Writing finished.");
                return false;
            }

            try
            {
                var ar0 = socket.BeginSend(buffer.Data, SocketFlags.None, ar =>
                {
                    if (ar.CompletedSynchronously) return;

                    server.Scheduler.Post(() =>
                    {
                        CompleteWrite(ar);

                        if (!DoWrite() && continuation != null)
                        {
                            var c = continuation;
                            continuation = null;
                            c();
                        }
                    });
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
                ExceptionEventArgs.Exception = new Exception("Exception on write.", e);
                OnError(this, ExceptionEventArgs);
                return false;
            }
        }

        void CompleteWrite(IAsyncResult ar)
        {
            try
            {
                if (disposed) return;

                var written = socket.EndSend(ar);
                buffer.Remove(written);

                Debug.WriteLine("Wrote " + written + " " + (ar.CompletedSynchronously ? "" : "a") + "sync, buffer size is " + buffer.Size);

                if (writeEnded)
                {
                    ShutdownIfBufferIsEmpty();
                }
            }
            catch (Exception e)
            {
                ExceptionEventArgs.Exception = new Exception("Exception during write callback.", e);
                OnError(this, ExceptionEventArgs);
            }
        }
        bool readZero;
        int reads;
        int readsCompleted;
        void DoRead()
        {
            if (inputBuffer == null)
                inputBuffer = new byte[1024 * 4];

            try
            {
                Debug.WriteLine("Reading.");
                reads++;
                socket.BeginReceive(inputBuffer, 0, inputBuffer.Length, SocketFlags.None, ar =>
                {
                    readsCompleted++;
                    Debug.WriteLine("Read callback.");
                    server.Scheduler.Post(() =>
                    {
                        if (disposed)
                            return;

                        int read = 0;
                        try
                        {
                            read = socket.EndReceive(ar);
                            Debug.WriteLine("Read " + read);
                        }
                        catch (Exception e)
                        {
                            ExceptionEventArgs.Exception = new Exception("Error while reading.", e);
                            OnError(this, ExceptionEventArgs);
                            return;
                        }

                        if (read == 0)
                        {
                            readZero = true;
                            RaiseEnd();
                        }
                        else
                        {
                            DataEventArgs.Data = new ArraySegment<byte>(inputBuffer, 0, read);
                            DataEventArgs.Continuation = DoRead;

                            OnData(this, DataEventArgs);

                            if (!DataEventArgs.WillInvokeContinuation)
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
                ExceptionEventArgs.Exception = new Exception("Error while reading.", e);
                OnError(this, ExceptionEventArgs);
            }
        }

        public void End()
        {
            if (disposed)
                throw new ObjectDisposedException("socket");

            //if (writeEnded)
            //    throw new InvalidOperationException("The socket was previously ended.");

            writeEnded = true;
            ShutdownIfBufferIsEmpty();
        }

        void HandleSocketException(SocketException e)
        {
            if (e.ErrorCode == 10053 || e.ErrorCode == 10054)
            {
                //Console.WriteLine("Connection " + id + ": peer reset (" + e.ErrorCode + ")");
                RaiseEnd();
                return;
            }

            ExceptionEventArgs.Exception = new Exception("SocketException while reading.", e);
            OnError(this, ExceptionEventArgs);
        }

        void ShutdownIfBufferIsEmpty()
        {
            if (buffer.Size == 0)
            {
                socket.Shutdown(SocketShutdown.Send);
                RaiseClosedIfNecessary();
                Debug.WriteLine("Shut down socket outgoing.");
            }
        }

        void RaiseClosedIfNecessary()
        {
            if (writeEnded && readEnded && !closed)
            {
                closed = true;
                OnClose(this, ExceptionEventArgs.Empty);
            }
        }

        void RaiseEnd()
        {
            readEnded = true;
            OnEnd(this, ExceptionEventArgs.Empty);
            RaiseClosedIfNecessary();
        }

        public void Dispose()
        {
            if (disposed)
                throw new ObjectDisposedException("socket");

            //if (reads == 0 || readsCompleted == 0 || !readZero)
            //    Console.WriteLine("Connection " + id + ": reset");

            Debug.WriteLine("Closing socket ");
            disposed = true;
            socket.Dispose();
            server.SocketClosed(this);
            server = null;
        }
    }
}
