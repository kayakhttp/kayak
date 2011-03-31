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
        bool disposed, connecting, connected;
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
            Debug.WriteLine("KayakSocket: connect called with " + ep);
            if (connecting)
                throw new InvalidOperationException("The socket was connecting.");

            if (connected)
                throw new InvalidOperationException("The socket was connected.");

            connecting = true;

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
                    connected = true;
                    connecting = false;

                    if (error is ObjectDisposedException)
                        return;

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
                var result = BeginSend();

                if (!result)
                {
                    this.continuation = null;
                }

                return result;
            }
        }

        bool BeginSend()
        {
            if (buffer.Size == 0 || disposed)
            {
                Debug.WriteLine("Writing finished.");
                return false;
            }

            try
            {
                int written = 0;
                Exception error;
                var ar0 = socket.BeginSend(buffer.Data, SocketFlags.None, ar =>
                {
                    if (ar.CompletedSynchronously) return;

                    written = EndSend(ar, out error);

                    scheduler.Post(() =>
                    {
                        HandleSendResult(written, error, false);

                        if (!BeginSend() && continuation != null)
                        {
                            var c = continuation;
                            continuation = null;
                            c();
                        }
                    });
                }, null);

                if (ar0.CompletedSynchronously)
                {
                    written = EndSend(ar0, out error);

                    if (error is ObjectDisposedException) 
                        return false;

                    HandleSendResult(written, error, true);

                    return BeginSend();
                }

                return true;
            }
            catch (Exception e)
            {
                RaiseError(new Exception("Exception on write.", e));
                return false;
            }
        }

        int EndSend(IAsyncResult ar, out Exception error)
        {
            error = null;
            try
            {
                return socket.EndSend(ar);
            }
            catch (Exception e)
            {
                error = e;
                return -1;
            }
        }

        void HandleSendResult(int written, Exception error, bool sync)
        {
            if (error != null)
            {
                RaiseError(new Exception("Exception during write callback.", error));
            }
            else
            {
                buffer.Remove(written);

                Debug.WriteLine("KayakSocket: Wrote " + written + " " + (sync ? "" : "a") + "sync, buffer size is " + buffer.Size);

                if (writeEnded)
                {
                    ShutdownIfBufferIsEmpty();
                }
            }
        }

        internal void DoRead()
        {
            if (readEnded == true)
                throw new Exception("DoRead called after reading ended.");

            if (inputBuffer == null)
                inputBuffer = new byte[1024 * 4];

            try
            {
                Exception error;
                int read;
                Debug.WriteLine("Reading.");
                var ar0 = socket.BeginReceive(inputBuffer, 0, inputBuffer.Length, SocketFlags.None, ar =>
                {
                    if (ar.CompletedSynchronously) return;

                    read = EndRead(ar, out error);
                    
                    scheduler.Post(() =>
                    {
                        HandleReadResult(read, error, true);
                    });

                }, null);

                if (ar0.CompletedSynchronously)
                {
                    read = EndRead(ar0, out error);
                    
                    if (error is ObjectDisposedException)
                        return;

                    Debug.WriteLine("KayakSocket: sync read " + read);
                    HandleReadResult(read, error, false);
                }
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

        int EndRead(IAsyncResult ar, out Exception error)
        {
            error = null;
            try
            {
                return socket.EndReceive(ar);
            }
            catch (Exception e)
            {
                error = e;
                return -1;
            }
        }

        void HandleReadResult(int read, Exception error, bool sync)
        {
            if (error != null)
            {
                RaiseError(new Exception("Error while reading.", error));
                return;
            }

            Debug.WriteLine("KayakSocket: " + (sync ? "" : "a") + "sync read " + read);

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
            connected = false;
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

            Debug.WriteLine("KayakSocket: dispose (server is " + ((server == null) ? "null?" : "not null") + ", id = " + id + ")");
            disposed = true;

            if (socket != null) // i. e., never connected
                socket.Dispose();

            if (server != null)
            {
                Debug.WriteLine("KayakSocket: informing server of demise");
                server.SocketClosed(this);
                server = null;
            }
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
