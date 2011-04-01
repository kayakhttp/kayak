using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace Kayak
{
    class ServerState
    {
        [Flags]
        enum State : int
        {
            None = 0,
            Listening = 1,
            Closing = 1 << 1,
            Closed = 1 << 2,
            Disposed = 1 << 3
        }

        State state;
        int connections;

        public ServerState()
        {
            state = State.None;
        }

        public void SetListening()
        {
            lock (this)
            {
                Debug.WriteLine("state is " + state);
                if ((state & State.Disposed) > 0)
                    throw new ObjectDisposedException(typeof(KayakServer).Name);

                if ((state & State.Listening) > 0)
                    throw new InvalidOperationException("The server was already listening.");
            
                if ((state & State.Closing) > 0)
                    throw new InvalidOperationException("The server was closing.");

                if ((state & State.Closed) > 0)
                    throw new InvalidOperationException("The server was closed.");

                state |= State.Listening;
            }
        }

        public void IncrementConnections()
        {
            lock (this)
            {
                connections++;
            }
        }

        public bool DecrementConnections()
        {
            lock (this)
            {
                connections--;

                if (connections == 0 && (state & State.Closing) > 0)
                {
                    state ^= State.Closing;
                    state |= State.Closed;
                    return true;
                }

                return false;
            }
        }

        public bool SetClosing()
        {
            lock (this)
            {
                if ((state & State.Disposed) > 0)
                    throw new ObjectDisposedException(typeof(KayakServer).Name);

                if (state == State.None)
                    throw new InvalidOperationException("The server was not listening.");
                
                if ((state & State.Listening) == 0)
                    throw new InvalidOperationException("The server was not listening.");
                
                if ((state & State.Closing) > 0)
                    throw new InvalidOperationException("The server was closing.");

                if ((state & State.Closed) > 0)
                    throw new InvalidOperationException("The server was closed.");

                if (connections == 0)
                {
                    state |= State.Closed;
                    return true;
                }
                else
                {
                    state |= State.Closing;
                }

                return true;
            }
        }

        public void SetError()
        {
            lock (this)
            {
                state = State.Closed;
            }
        }

        public void SetDisposed()
        {
            lock (this)
            {
                if ((state & State.Disposed) > 0)
                    throw new ObjectDisposedException(typeof(KayakServer).Name);

                Debug.WriteLine("Disposing!?");
                state |= State.Disposed;
            }
        }
    }
    public class KayakServer : IServer
    {
        public event EventHandler<ConnectionEventArgs> OnConnection;
        public event EventHandler OnClose;

        public IPEndPoint ListenEndPoint { get { return (IPEndPoint)listener.LocalEndPoint; } }

        IScheduler scheduler;
        ServerState state;
        Socket listener;

        public KayakServer(IScheduler scheduler)
        {
            this.scheduler = scheduler;
            listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.IP);
            state = new ServerState();
        }

        public void Dispose()
        {
            state.SetDisposed();

            if (listener != null)
            {
                listener.Dispose();
            }
        }

        public void Listen(IPEndPoint ep)
        {
            state.SetListening();

            Debug.WriteLine("KayakServer binding to " + ListenEndPoint);

            listener.Bind(ep);
            listener.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReceiveTimeout, 10000);
            listener.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.SendTimeout, 10000);
            listener.Listen(1000);

            AcceptNext();
        }

        public void Close()
        {
            var closed = state.SetClosing();
            
            Console.WriteLine("Closing listening socket.");
            listener.Close();

            if (closed)
                RaiseOnClose();
        }

        internal void SocketClosed(KayakSocket socket)
        {
            //Debug.WriteLine("Connection " + socket.id + ": closed (" + connections + " active connections)");
            if (state.DecrementConnections())
                RaiseOnClose();
        }

        void RaiseOnClose()
        {
            if (OnClose != null)
                OnClose(this, EventArgs.Empty);
        }

        void AcceptNext()
        {
            try
            {
                Debug.WriteLine("BeginAccept");
                listener.BeginAccept(iasr =>
                {
                    Socket socket = null;
                    Exception error = null;
                    try
                    {
                        Debug.WriteLine("EndAccept");
                        socket = listener.EndAccept(iasr);
                        AcceptNext();
                    }
                    catch (Exception e)
                    {
                        error = e;
                    }

                    if (error is ObjectDisposedException)
                        return;

                    //scheduler.Post(() =>
                    //{
                        if (error != null)
                            HandleAcceptError(error);

                        var s = new KayakSocket(socket, this.scheduler);
                        if (OnConnection != null)
                        {
                            state.IncrementConnections();
                            OnConnection(this, new ConnectionEventArgs(s));
                            s.DoRead(); // TODO defer til OnData event gets listener
                        }
                        else
                        {
                            s.End();
                            s.Dispose();
                        }
                    //});

                }, null);
            }
            catch (ObjectDisposedException e)
            {
                return;
            }
            catch (Exception e)
            {
                HandleAcceptError(e);
            }
        }

        void HandleAcceptError(Exception e)
        {
            state.SetError();

            try
            {
                listener.Close();
            }
            catch { }

            Debug.WriteLine("Error attempting to accept connection.");
            e.PrintStacktrace();

            if (OnClose != null)
                OnClose(this, EventArgs.Empty);
        }
    }
}
