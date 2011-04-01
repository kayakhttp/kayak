using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;

namespace Kayak
{
    class KayakServerState
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

        public KayakServerState()
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
}
