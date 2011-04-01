using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Diagnostics;
using System.Threading;

namespace Kayak
{
    interface ISocketWrapper : IDisposable
    {
        IAsyncResult BeginConnect(IPEndPoint ep, AsyncCallback callback);
        void EndConnect(IAsyncResult iasr);

        IAsyncResult BeginReceive(byte[] buffer, int offset, int count, AsyncCallback callback);
        int EndReceive(IAsyncResult iasr);

        IAsyncResult BeginSend(List<ArraySegment<byte>> data, AsyncCallback callback);
        int EndSend(IAsyncResult iasr);

        void Shutdown();
    }

    class SocketState
    {
        [Flags]
        enum State : int
        {
            NotConnected = 1,
            Connecting = 1 << 1,
            Connected = 1 << 2,
            WriteEnded = 1 << 3,
            ReadEnded = 1 << 4,
            Closed = 1 << 5,
            Disposed = 1 << 6
        }

        State state;

        public SocketState(bool connected)
        {
            state = connected ? State.NotConnected : State.Connected;
        }

        public void SetConnecting()
        {
            lock (this)
            {
                if ((state & State.Disposed) > 0)
                    throw new ObjectDisposedException(typeof(KayakSocket).Name);

                if ((state & State.Connected) > 0)
                    throw new InvalidOperationException("The socket was connected.");

                if ((state & State.Connecting) > 0)
                    throw new InvalidOperationException("The socket was connecting.");

                state |= State.Connecting;
            }
        }

        public void SetConnected()
        {
            lock (this)
            {
                // these checks should never pass; they are here for safety.
                if ((state & State.Disposed) > 0)
                    throw new ObjectDisposedException(typeof(KayakSocket).Name);

                if ((state & State.Connecting) == 0)
                    throw new Exception("The socket was not connecting.");

                state ^= State.Connecting;
                state |= State.Connected;
            }
        }


        //public bool IsWriteEnded()
        //{
        //    return writeEnded == 1;
        //}

        public void EnsureCanWrite()
        {
            lock (this)
            {
                if ((state & State.Disposed) > 0)
                    throw new ObjectDisposedException(typeof(KayakSocket).Name);

                if ((state & State.Connected) == 0)
                    throw new InvalidOperationException("The socket was not connected.");

                if ((state & State.WriteEnded) > 0)
                    throw new InvalidOperationException("The socket was previously ended.");
            }
        }

        public void EnsureCanRead()
        {
            lock (this)
            {
                // these checks should never pass; they are here for safety.
                if ((state & State.Disposed) > 0)
                    throw new ObjectDisposedException(typeof(KayakSocket).Name);

                if ((state & State.Connected) == 0)
                    throw new InvalidOperationException("The socket was not connected.");

                if ((state & State.ReadEnded) > 0)
                    throw new InvalidOperationException("The socket was previously ended by the peer.");
            }
        }

        public bool SetReadEnded()
        {
            lock (this)
            {
                EnsureCanRead();

                state |= State.ReadEnded;

                if ((state & State.WriteEnded) > 0)
                {
                    state |= State.Closed;
                    return true;
                }
                else
                    return false;
            }
        }

        public bool WriteCompleted()
        {
            lock (this)
            {
                if ((state & State.ReadEnded) > 0 & (state & State.WriteEnded) > 0)
                {
                    state |= State.Closed;
                    return true;
                }
                else
                    return false;
            }
        }

        public bool SetWriteEnded()
        {
            lock (this)
            {
                if ((state & State.Disposed) > 0)
                    throw new ObjectDisposedException(typeof(KayakSocket).Name);

                if ((state & State.Connected) == 0)
                    throw new InvalidOperationException("The socket was not connected.");

                if ((state & State.WriteEnded) > 0)
                    throw new InvalidOperationException("The socket was previously ended.");

                state |= State.WriteEnded;

                if ((state & State.ReadEnded) > 0)
                {
                    state |= State.Closed;
                    return true;
                }
                else
                    return false;
            }
        }

        public void SetError()
        {
            lock (this)
            {
                if ((state & State.Disposed) > 0)
                    throw new ObjectDisposedException(typeof(KayakSocket).Name);

                state ^= State.Connecting | State.Connected;
                state |= State.Closed;
            }
        }

        public void SetDisposed()
        {
            lock (this)
            {
                if ((state & State.Disposed) > 0)
                    throw new ObjectDisposedException(typeof(KayakSocket).Name);

                state |= State.Disposed;
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

        public int id;
        static int nextId;

        public IPEndPoint RemoteEndPoint { get; private set; }

        SocketBuffer buffer;

        byte[] inputBuffer;

        SocketState state;

        SocketWrapper socket;
        Action continuation;
        IScheduler scheduler;

        public KayakSocket(IScheduler scheduler)
        {
            this.scheduler = scheduler;
            state = new SocketState(true);
        }

        internal KayakSocket(Socket socket, IScheduler scheduler)
        {
            this.id = nextId++;
            this.socket = new SocketWrapper(socket);
            this.scheduler = scheduler;
            state = new SocketState(false);
        }

        public void Connect(IPEndPoint ep)
        {
            Debug.WriteLine("KayakSocket: connect called with " + ep);

            state.SetConnecting();

            Debug.WriteLine("KayakSocket: connecting to " + ep);
            this.socket = new SocketWrapper(ep.Address.AddressFamily);

            socket.BeginConnect(ep, iasr => 
            {
                Exception error = null;

                try
                {
                    socket.EndConnect(iasr);
                }
                catch (Exception e)
                {
                    error = e;
                }
                
                //scheduler.Post(() =>
                //{
                    if (error is ObjectDisposedException)
                        return;

                    if (error != null)
                    {
                        state.SetError();

                        Debug.WriteLine("KayakSocket: error while connecting to " + ep);
                        RaiseError(error);
                    }
                    else
                    {
                        state.SetConnected();

                        Debug.WriteLine("KayakSocket: connected to " + ep);
                        if (OnConnected != null)
                            OnConnected(this, EventArgs.Empty);

                        DoRead();
                    }
                //});
            });
        }

        public bool Write(ArraySegment<byte> data, Action continuation)
        {
            state.EnsureCanWrite();

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

                if (this.continuation == null)
                    result = false;
                else if (!result)
                {
                    this.continuation = null;
                }

                return result;
            }
        }

        bool BeginSend()
        {
            if (buffer.Size == 0)
            {
                Debug.WriteLine("Writing finished.");
                return false;
            }

            try
            {
                int written = 0;
                Exception error;
                var ar0 = socket.BeginSend(buffer.Data, ar =>
                {
                    if (ar.CompletedSynchronously) 
                        return;

                    written = EndSend(ar, out error);

                    if (error is ObjectDisposedException) 
                        return;

                    //scheduler.Post(() =>
                    //{
                        if (error != null)
                            HandleSendError(error);
                        else
                            HandleSendResult(written, false);

                        if (!BeginSend() && continuation != null)
                        {
                            var c = continuation;
                            continuation = null;
                            c();
                        }
                    //});
                });

                if (ar0.CompletedSynchronously)
                {
                    written = EndSend(ar0, out error);

                    if (error is ObjectDisposedException) 
                        return false;

                    if (error != null)
                        HandleSendError(error);
                    else
                        HandleSendResult(written, true);

                    return BeginSend();
                }

                return true;
            }
            catch (Exception e)
            {
                HandleSendError(e);
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

        void HandleSendResult(int written, bool sync)
        {
            buffer.Remove(written);

            Debug.WriteLine("KayakSocket: Wrote " + written + " " + (sync ? "" : "a") + "sync, buffer size is " + buffer.Size);

            if (BufferIsEmpty() && state.WriteCompleted())
            {
                if (socket != null)
                    socket.Shutdown();

                RaiseClosed();
            }
        }

        void HandleSendError(Exception error)
        {
            state.SetError();
            RaiseError(new Exception("Exception on write.", error));
        }

        internal void DoRead()
        {
            state.EnsureCanRead();

            if (inputBuffer == null)
                inputBuffer = new byte[1024 * 4];

            try
            {
                int read;
                Exception error;
                Debug.WriteLine("KayakSocket: reading.");
                var ar0 = socket.BeginReceive(inputBuffer, 0, inputBuffer.Length, ar =>
                {
                    if (ar.CompletedSynchronously) return;
                    Debug.WriteLine("KayakSocket: receive completed async");

                    read = EndRead(ar, out error);

                    if (error is ObjectDisposedException)
                        return;
                    
                    //scheduler.Post(() =>
                    //{
                        if (error != null)
                        {
                            HandleReadError(error);
                        }
                        else
                        {
                            HandleReadResult(read, true);
                        }
                    //});

                });

                if (ar0.CompletedSynchronously)
                {
                    Debug.WriteLine("KayakSocket: receive completed sync");
                    read = EndRead(ar0, out error);

                    if (error is ObjectDisposedException)
                        return;

                    if (error != null)
                    {
                        HandleReadError(error);
                    }
                    else
                    {
                        HandleReadResult(read, false);
                    }
                }
            }
            catch (Exception e)
            {
                HandleReadError(e);
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

        void HandleReadResult(int read, bool sync)
        {
            Debug.WriteLine("KayakSocket: " + (sync ? "" : "a") + "sync read " + read);

            if (read == 0)
            {
                PeerHungUp();
            }
            else
            {
                var dataEventArgs = new DataEventArgs()
                {
                    Data = new ArraySegment<byte>(inputBuffer, 0, read),
                    Continuation = DoRead
                };

                if (OnData != null)
                    OnData(this, dataEventArgs);

                if (!dataEventArgs.WillInvokeContinuation)
                    DoRead();
            }
        }

        void HandleReadError(Exception e)
        {
            if (e is ObjectDisposedException)
                return;

            Debug.WriteLine("KayakSocket: read error");

            if (e is SocketException)
            {
                var socketException = e as SocketException;

                if (socketException.ErrorCode == 10053 || socketException.ErrorCode == 10054)
                {
                    Console.WriteLine("KayakSocket: peer reset (" + socketException.ErrorCode + ")");
                    PeerHungUp();
                    return;
                }
            }

            state.SetError();

            RaiseError(new Exception("Error while reading.", e));
        }

        public void End()
        {
            Debug.WriteLine("KayakSocket: end");

            var empty = BufferIsEmpty();

            if (empty)
                socket.Shutdown();

            if (state.SetWriteEnded())
            {
                RaiseClosed();
            }
        }

        public void Dispose()
        {
            state.SetDisposed();

            //if (reads == 0 || readsCompleted == 0 || !readZero)
            //    Console.WriteLine("Connection " + id + ": reset");

            if (socket != null) // i. e., never connected
                socket.Dispose();
        }

        void PeerHungUp()
        {
            Debug.WriteLine("KayakSocket: peer hung up.");
            var close = state.SetReadEnded();

            if (OnEnd != null)
                OnEnd(this, EventArgs.Empty);

            if (close)
                RaiseClosed();
        }

        bool BufferIsEmpty()
        {
            return socket != null && (buffer == null || buffer.Size == 0);
        }

        void RaiseError(Exception e)
        {
            if (OnError != null)
                OnError(this, new ExceptionEventArgs() { Exception = e });

            RaiseClosed();
        }

        void RaiseClosed()
        {
            if (OnClose != null)
                OnClose(this, ExceptionEventArgs.Empty);
        }

        class SocketWrapper : ISocketWrapper
        {
            Socket socket;

            public SocketWrapper(AddressFamily af)
                : this(new Socket(af, SocketType.Stream, ProtocolType.Tcp)) { }

            public SocketWrapper(Socket socket)
            {
                this.socket = socket;
            }
            public IAsyncResult BeginConnect(IPEndPoint ep, AsyncCallback callback)
            {
                return socket.BeginConnect(ep, callback, null);
            }

            public void EndConnect(IAsyncResult iasr)
            {
                socket.EndConnect(iasr);
            }

            public IAsyncResult BeginReceive(byte[] buffer, int offset, int count, AsyncCallback callback)
            {
                return socket.BeginReceive(buffer, offset, count, SocketFlags.None, callback, null);
            }

            public int EndReceive(IAsyncResult iasr)
            {
                return socket.EndReceive(iasr);
            }

            public IAsyncResult BeginSend(List<ArraySegment<byte>> data, AsyncCallback callback)
            {
                return socket.BeginSend(data, SocketFlags.None, callback, null);
            }

            public int EndSend(IAsyncResult iasr)
            {
                return socket.EndSend(iasr);
            }

            public void Shutdown()
            {
                socket.Shutdown(SocketShutdown.Send);
            }

            public void Dispose()
            {
                socket.Dispose();
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
