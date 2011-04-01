using System;

namespace Kayak
{
    class KayakSocketState
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

        public KayakSocketState(bool connected)
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
}
