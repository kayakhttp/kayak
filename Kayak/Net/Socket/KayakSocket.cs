using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;

namespace Kayak
{
    class KayakSocket : ISocket
    {
        internal ISocketDelegate del;

        public int id;
        static int nextId;

        public IPEndPoint RemoteEndPoint { get; private set; }

        OutputBuffer buffer;

        byte[] inputBuffer;

        KayakSocketState state;

        SocketWrapper socket;
        Action continuation;
        IScheduler scheduler;

        public KayakSocket(ISocketDelegate del, IScheduler scheduler)
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
            state.BeginWrite(data.Count > 0);

            if (data.Count == 0) return false;

            if (this.continuation != null) 
                throw new InvalidOperationException("Write was pending.");

            if (buffer == null)
                buffer = new OutputBuffer();

            var bufferSize = buffer.Size;

            // XXX copy! could optimize here?
            buffer.Add(data);
            Debug.WriteLine("KayakSocket: added " + data.Count + " bytes to buffer, buffer size was " + bufferSize + ", buffer size is " + buffer.Size);

            if (bufferSize > 0)
            {
                // we're between an async beginsend and endsend,
                // and user did not provide continuation

                if (continuation != null)
                {
                    this.continuation = continuation;
                    return true;
                }
                else
                    return false;
            }
            else
            {
                var result = BeginSend();

                // tricky: potentially throwing away fact that send will complete async
                if (continuation == null)
                    result = false;

                if (result)
                    this.continuation = continuation;

                return result;
            }
        }

        bool BeginSend()
        {
            while (true)
            {
                if (BufferIsEmpty())
                    break;

                int written = 0;
                Exception error;
                IAsyncResult ar0;

                try
                {
                    ar0 = socket.BeginSend(buffer.Data, ar =>
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
                }
                catch (Exception e)
                {
                    if (!(e is ObjectDisposedException))
                        HandleSendError(e);

                    break;
                }

                if (!ar0.CompletedSynchronously)
                    return true;

                written = EndSend(ar0, out error);

                if (error is ObjectDisposedException)
                    break;

                if (error != null)
                {
                    HandleSendError(error);
                    break;
                }
                else
                    HandleSendResult(written, true);
            }

            return false;
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

            bool shutdownSocket = false;
            bool raiseClosed = false;

            state.EndWrite(BufferIsEmpty(), out shutdownSocket, out raiseClosed);

            if (shutdownSocket)
            {
                Debug.WriteLine("KayakSocket: shutting down socket after send.");
                socket.Shutdown();
            }

            if (raiseClosed)
                RaiseClosed();
        }

        void HandleSendError(Exception error)
        {
            state.SetError();
            RaiseError(new Exception("Exception on write.", error));
        }

        internal void DoRead()
        {
            if (inputBuffer == null)
                inputBuffer = new byte[1024 * 4];

            while (true)
            {
                if (!state.CanRead()) return;

                int read;
                Exception error;
                IAsyncResult ar0 = null;

                Debug.WriteLine("KayakSocket: reading.");

                try
                {
                    ar0 = socket.BeginReceive(inputBuffer, 0, inputBuffer.Length, ar =>
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
                                if (!HandleReadResult(read, false))
                                    scheduler.Post(DoRead);
                            }
                        });
                    });
                }
                catch (Exception e)
                {
                    if (!(e is ObjectDisposedException))
                        HandleSendError(e);

                    break;
                }

                if (!ar0.CompletedSynchronously)
                    break;

                Debug.WriteLine("KayakSocket: receive completed sync");
                read = EndRead(ar0, out error);

                if (error is ObjectDisposedException)
                    break;

                if (error != null)
                {
                    HandleReadError(error);
                    break;
                }
                else
                {
                    if (HandleReadResult(read, true))
                        break;
                }
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

        bool HandleReadResult(int read, bool sync)
        {
            Debug.WriteLine("KayakSocket: " + (sync ? "" : "a") + "sync read " + read);

            if (read == 0)
            {
                PeerHungUp();
                return false;
            }
            else
            {
                return del.OnData(this, new ArraySegment<byte>(inputBuffer, 0, read), DoRead);
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
                    Debug.WriteLine("KayakSocket: peer reset (" + socketException.ErrorCode + ")");
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

            bool shutdownSocket = false;
            bool raiseClosed = false;
            
            state.SetEnded(out shutdownSocket, out raiseClosed);

            if (shutdownSocket)
            {
                Debug.WriteLine("KayakSocket: shutting down socket on End.");
                socket.Shutdown();
            }

            if (raiseClosed)
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
            bool raiseClosed = false;
            state.SetReadEnded(out raiseClosed);

            del.OnEnd(this);

            if (raiseClosed)
                RaiseClosed();
        }

        bool BufferIsEmpty()
        {
            return socket != null && (buffer == null || buffer.Size == 0);
        }

        void RaiseError(Exception e)
        {
            Debug.WriteLine("KayakSocket: raising OnError");
            del.OnError(this, e);

            RaiseClosed();
        }

        void RaiseClosed()
        {
            Debug.WriteLine("KayakSocket: raising OnClose");
            del.OnClose(this);
        }
    }
}
