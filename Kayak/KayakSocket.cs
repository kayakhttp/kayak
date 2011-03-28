using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Diagnostics;

namespace Kayak
{
    class KayakSocket : ISocket
    {
        public event EventHandler OnConnected;
        public event EventHandler<DataEventArgs> OnData;
        public event EventHandler OnEnd;
        public event EventHandler OnTimeout;
        public event EventHandler<ExceptionEventArgs> OnError;
        public event EventHandler OnClose;

        [ThreadStatic]
        static ExceptionEventArgs ExceptionEventArgs;

        [ThreadStatic]
        static DataEventArgs DataEventArgs;

        internal static void InitEvents()
        {
            DataEventArgs = new DataEventArgs();
            ExceptionEventArgs = new ExceptionEventArgs();
        }

        public int id;
        static int nextId;

        public IPEndPoint RemoteEndPoint { get; private set; }

        SocketBuffer buffer;

        byte[] inputBuffer;
        Socket socket;
        bool disposed;
        bool writeEnded, readEnded, closed;
        Action continuation;
        KayakServer server;
        IScheduler scheduler;

        public KayakSocket(IScheduler scheduler)
        {
            this.scheduler = scheduler;
        }

        internal KayakSocket(Socket socket, KayakServer server, IScheduler scheduler)
        {
            this.id = nextId++;
            this.socket = socket;
            this.server = server;
            this.scheduler = scheduler;
        }

        public void Connect(IPEndPoint ep)
        {
            Debug.WriteLine("KayakSocket: connecting to " + ep);
            this.socket = new Socket(ep.Address.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            socket.BeginConnect(ep, iasr => {
                Exception error = null;

                try
                {
                    socket.EndConnect(iasr);
                }
                catch (Exception e)
                {
                    error = e;
                }

                scheduler.Post(() =>
                {
                    if (error != null)
                    {
                        Debug.WriteLine("KayakSocket: error while connecting to " + ep);
                        RaiseError(error);
                    }
                    else
                    {
                        Debug.WriteLine("KayakSocket: connected to " + ep);
                        if (OnConnected != null)
                            OnConnected(this, EventArgs.Empty);

                        DoRead();
                    }
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

            if (buffer == null)
                buffer = new SocketBuffer();

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
                    this.continuation = null;
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

                    scheduler.Post(() =>
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
                RaiseError(new Exception("Exception on write.", e));
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

                Debug.WriteLine("KayakSocket: Wrote " + written + " " + (ar.CompletedSynchronously ? "" : "a") + "sync, buffer size is " + buffer.Size);

                if (writeEnded)
                {
                    ShutdownIfBufferIsEmpty();
                }
            }
            catch (Exception e)
            {
                RaiseError(new Exception("Exception during write callback.", e));
            }
        }
        int reads;
        int readsCompleted;
        internal void DoRead()
        {
            if (inputBuffer == null)
                inputBuffer = new byte[1024 * 4];

            try
            {
                //Debug.WriteLine("Reading.");
                reads++;
                socket.BeginReceive(inputBuffer, 0, inputBuffer.Length, SocketFlags.None, ar =>
                {
                    readsCompleted++;
                    //Debug.WriteLine("Read callback.");

                    int read = 0;
                    Exception error = null;

                    try
                    {
                        read = socket.EndReceive(ar);
                        Debug.WriteLine("KayakSocket: Read " + read);
                    }
                    catch (Exception e)
                    {
                        error = e;
                    }

                    scheduler.Post(() =>
                    {
                        if (disposed)
                            return;

                        if (error != null)
                        {
                            RaiseError(new Exception("Error while reading.", error));
                            return;
                        }

                        if (read == 0)
                        {
                            RaiseEnd();
                        }
                        else
                        {
                            DataEventArgs.Data = new ArraySegment<byte>(inputBuffer, 0, read);
                            DataEventArgs.Continuation = DoRead;

                            if (OnData != null)
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
                RaiseError(new Exception("Error while reading.", e));
            }
        }

        public void End()
        {
            if (disposed)
                throw new ObjectDisposedException("socket");

            //if (writeEnded)
            //    throw new InvalidOperationException("The socket was previously ended.");

            Debug.WriteLine("KayakSocket: end");
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

            RaiseError(new Exception("SocketException while reading.", e));
        }

        void ShutdownIfBufferIsEmpty()
        {
            if (buffer == null || buffer.Size == 0)
            {
                Debug.WriteLine("KayakSocket: sending FIN packet");
                socket.Shutdown(SocketShutdown.Send);
                RaiseClosedIfNecessary();
            }
        }

        void RaiseClosedIfNecessary()
        {
            if (writeEnded && readEnded && !closed)
                RaiseClosed();
        }

        void RaiseClosed()
        {
            closed = true;
            if (OnClose != null)
                OnClose(this, ExceptionEventArgs.Empty);
        }

        void RaiseError(Exception e)
        {
            ExceptionEventArgs.Exception = e;

            if (OnError != null)
                OnError(this, ExceptionEventArgs);

            RaiseClosed();
        }

        void RaiseEnd()
        {
            readEnded = true;
            if (OnEnd != null)
                OnEnd(this, ExceptionEventArgs.Empty);

            RaiseClosedIfNecessary();
        }

        public void Dispose()
        {
            if (disposed)
                throw new ObjectDisposedException("socket");

            //if (reads == 0 || readsCompleted == 0 || !readZero)
            //    Console.WriteLine("Connection " + id + ": reset");

            Debug.WriteLine("KayakSocket: dispose");
            disposed = true;

            if (socket != null) // i. e., never connected
                socket.Dispose();

            if (server != null)
            {
                server.SocketClosed(this);
                server = null;
            };
        }

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
    }
}
