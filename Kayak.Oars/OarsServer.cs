using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using Oars;

namespace Kayak.Oars
{
    public class OarsServer : IServer
    {
        //public event EventHandler<ConnectionEventArgs> OnConnection;
        //public event EventHandler OnClose;

        public IPEndPoint ListenEndPoint { get; private set; }
        EventBase eventBase;

        int connections;
        ConnectionListener listener;
        bool listening;
        bool closed;

        public OarsServer(EventBase eventBase)
        {
            this.eventBase = eventBase;
        }

        public void Dispose()
        {
            listener.Dispose();
            listener = null;
        }

        public IDisposable Listen(IPEndPoint ep)
        {
            if (listening)
                throw new InvalidOperationException("Already listening.");

            ListenEndPoint = ep;

            listener = new ConnectionListener(eventBase, ep, 1000);
            listener.Enable();
            listener.ConnectionAccepted = (fd, remoteEp) =>
            {
                connections++;
                var ev = new Event(eventBase, fd, Events.EV_WRITE | Events.EV_READ);

                //if (OnConnection != null)
                //    OnConnection(this, new ConnectionEventArgs(new OarsSocket(ev, remoteEp, this)));
            };
            listening = true;
            closed = false;

            // XXX return Disposable(() => Close());
            return null;
        }

        public void Close()
        {
            if (!listening)
                throw new InvalidOperationException("Not listening.");

            if (closed)
                throw new InvalidOperationException("Already closed.");

            Dispose();

            listening = false;
            closed = true;

            RaiseOnClosedIfNecessary();
        }

        internal void SocketClosed()
        {
            connections--;
            RaiseOnClosedIfNecessary();
        }

        void RaiseOnClosedIfNecessary()
        {
            if (closed && connections == 0)
            {
                closed = false;
                //if (OnClose != null)
                //    OnClose(this, EventArgs.Empty);
            }
        }
    }
}
