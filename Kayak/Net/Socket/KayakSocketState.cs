using System;
using System.Diagnostics;

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
            if ((state & State.Disposed) > 0)
                throw new ObjectDisposedException(typeof(KayakSocket).Name);

            if ((state & State.Connected) > 0)
                throw new InvalidOperationException("The socket was connected.");

            if ((state & State.Connecting) > 0)
                throw new InvalidOperationException("The socket was connecting.");

            state |= State.Connecting;
        }

        public void SetConnected()
        {
            // these checks should never pass; they are here for safety.
            if ((state & State.Disposed) > 0)
                throw new ObjectDisposedException(typeof(KayakSocket).Name);

            if ((state & State.Connecting) == 0)
                throw new Exception("The socket was not connecting.");

            state ^= State.Connecting;
            state |= State.Connected;
        }


        //public bool IsWriteEnded()
        //{
        //    return writeEnded == 1;
        //}

        public void EnsureCanWrite()
        {
            if ((state & State.Disposed) > 0)
                throw new ObjectDisposedException("KayakSocket");

            if ((state & State.Connected) == 0)
                throw new InvalidOperationException("The socket was not connected.");

            if ((state & State.WriteEnded) > 0)
                throw new InvalidOperationException("The socket was previously ended.");
        }

        public void EnsureCanRead()
        {
            // these checks should never pass; they are here for safety.
            
            // disabled, after the callback for an async read is raised, the socket 
            // may have been disposed, but there's no clean way for us to know this
            // short of duplicating the System.Net.Sockets.Socket's state. instead
            // we always loop and let it raise ObjectDisposedException, which we
            // handle more cleanly.
            //if ((state & State.Disposed) > 0)
            //        throw new ObjectDisposedException(typeof(KayakSocket).Name);

            if ((state & State.Connected) == 0)
                throw new InvalidOperationException("The socket was not connected.");

            if ((state & State.ReadEnded) > 0)
                throw new InvalidOperationException("The socket was previously ended by the peer.");
        }

        public bool SetReadEnded()
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

        public bool WriteCompleted(out bool writeEnded)
        {
            bool readEnded = (state & State.ReadEnded) > 0;
            writeEnded = (state & State.WriteEnded) > 0;

            Debug.WriteLine("KayakSocketState: WriteCompleted (readEnded = " + readEnded +
                ", writeEnded = " + writeEnded + ")");

            if (readEnded && writeEnded)
            {
                state |= State.Closed;
                return true;
            }
            else
                return false;
        }

        public bool SetWriteEnded()
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

        public void SetError()
        {
            if ((state & State.Disposed) > 0)
                throw new ObjectDisposedException(typeof(KayakSocket).Name);

            state ^= State.Connecting | State.Connected;
            state |= State.Closed;
        }

        public void SetDisposed()
        {
            //if ((state & State.Disposed) > 0)
            //    throw new ObjectDisposedException(typeof(KayakSocket).Name);

            state |= State.Disposed;
        }
    }
}
