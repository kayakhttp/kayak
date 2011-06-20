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
            Disposed = 1 << 6,
            BufferIsNotEmpty = 1 << 7
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

        public void BeginWrite(bool nonZeroData)
        {
            if ((state & State.Disposed) > 0)
                throw new ObjectDisposedException("KayakSocket");

            if ((state & State.Connected) == 0)
                throw new InvalidOperationException("The socket was not connected.");

            if ((state & State.WriteEnded) > 0)
                throw new InvalidOperationException("The socket was previously ended.");

            if (nonZeroData)
                state |= State.BufferIsNotEmpty;
        }


        void CanShutdownAndClose(out bool shutdownSocket, out bool raiseClosed)
        {
            bool bufferIsEmpty = (state & State.BufferIsNotEmpty) == 0;
            bool readEnded = (state & State.ReadEnded) > 0;
            bool writeEnded = (state & State.WriteEnded) > 0;

            Debug.WriteLine("KayakSocketState: CanShutdownAndClose (readEnded = " + readEnded +
                ", writeEnded = " + writeEnded +
                ", bufferIsEmpty = " + bufferIsEmpty + ")");

            shutdownSocket = writeEnded && bufferIsEmpty;

            if (readEnded && shutdownSocket)
            {
                state |= State.Closed;
                raiseClosed = true;
            }
            else
                raiseClosed = false;
        }
        public void EndWrite(bool bufferIsEmpty, out bool shutdownSocket, out bool raiseClosed)
        {
            if (bufferIsEmpty)
                state ^= State.BufferIsNotEmpty;

            CanShutdownAndClose(out shutdownSocket, out raiseClosed);
        }

        // okay, so.
        //
        // need to check this every time we're about to do a read.
        // since we potentially do this in a loop, we return false
        // to indicate that the loop should break out. however, if the 
        // socket was never connected...well, that's an error, bro.
        public bool CanRead()
        {
            if ((state & State.Connected) == 0)
                throw new InvalidOperationException("The socket was not connected.");

            if ((state & State.ReadEnded) > 0)
                return false;

            return true;
        }

        public void SetReadEnded(out bool raiseClosed)
        {
            state |= State.ReadEnded;

            if ((state & State.WriteEnded) > 0 && (state & State.BufferIsNotEmpty) == 0)
            {
                state |= State.Closed;
                raiseClosed = true;
            }
            else
                raiseClosed = false;
        }

        public void SetEnded(out bool shutdownSocket, out bool raiseClosed)
        {
            if ((state & State.Disposed) > 0)
                throw new ObjectDisposedException(typeof(KayakSocket).Name);

            if ((state & State.Connected) == 0)
                throw new InvalidOperationException("The socket was not connected.");

            if ((state & State.WriteEnded) > 0)
                throw new InvalidOperationException("The socket was previously ended.");

            state |= State.WriteEnded;

            CanShutdownAndClose(out shutdownSocket, out raiseClosed);
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
