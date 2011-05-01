using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;

namespace Kayak
{
    class KayakSocket : ISocket
    {
        //public event EventHandler OnConnected;
        //public event EventHandler<DataEventArgs> OnData;
        //public event EventHandler OnEnd;
        //public event EventHandler OnTimeout;
        //public event EventHandler<ExceptionEventArgs> OnError;
        //public event EventHandler OnClose;
        ISocketDelegate del;

        public int id;
        static int nextId;

        public IPEndPoint RemoteEndPoint { get; private set; }

        OutputBuffer buffer;

        byte[] inputBuffer;

        KayakSocketState state;

        SocketWrapper socket;
        Action continuation;
        IScheduler scheduler;

        public KayakSocket(IScheduler scheduler, ISocketDelegate del)
        {
            this.scheduler = scheduler;
            this.del = del;
            state = new KayakSocketState(true);
        }

        internal KayakSocket(Socket socket, IScheduler scheduler)
        {
            this.id = nextId++;
            this.socket = new SocketWrapper(socket);
            this.scheduler = scheduler;
            state = new KayakSocketState(false);
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
                
                scheduler.Post(() =>
                {
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

                        del.OnConnected(this);

                        DoRead();
                    }
                });
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
                buffer = new OutputBuffer();

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

                    scheduler.Post(() =>
                    {
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
                    });
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

            bool writeEnded = false;

            bool closed = state.WriteCompleted(out writeEnded);
            if (writeEnded && socket != null && BufferIsEmpty())
            {
                Debug.WriteLine("KayakSocket: shutting down after send.");
                socket.Shutdown();
            }

            if (closed)
                RaiseClosed();
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

                    read = EndRead(ar, out error);

                    if (error is ObjectDisposedException)
                        return;
                    
                    scheduler.Post(() =>
                    {
                        Debug.WriteLine("KayakSocket: receive completed async");

                        if (error != null)
                        {
                            HandleReadError(error);
                        }
                        else
                        {
                            HandleReadResult(read, true);
                        }
                    });

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
                if (!del.OnData(this, new ArraySegment<byte>(inputBuffer, 0, read), DoRead))
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
            {
                Debug.WriteLine("KayakSocket: shutting down socket on end.");
                socket.Shutdown();
            }

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

            del.OnEnd(this);

            if (close)
                RaiseClosed();
        }

        bool BufferIsEmpty()
        {
            return socket != null && (buffer == null || buffer.Size == 0);
        }

        void RaiseError(Exception e)
        {
            del.OnError(this, e);

            RaiseClosed();
        }

        void RaiseClosed()
        {
            del.OnClose(this);
        }
    }
}
