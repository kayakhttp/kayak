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
        public short Backlog;
        public IServerDelegate Delegate { get; set; }
        public IPEndPoint ListenEndPoint { get; private set; }

        EventBase eventBase;
        ConnectionListener listener;
        int connections;
        bool closed;

        public OarsServer(EventBase eventBase)
        {
            this.eventBase = eventBase;
        }


        public void Listen(IPEndPoint ep)
        {
            listener = new ConnectionListener(eventBase, ep, Backlog);
            listener.Enable();
            listener.ConnectionAccepted = (fd, remoteEp) =>
                {
                    connections++;
                    var ev = new Event(eventBase, fd, Events.EV_WRITE | Events.EV_READ);
                    Delegate.OnConnection(this, new OarsSocket2(ev, remoteEp, this));
                };
        }

        public void Close()
        {
            closed = true;
            listener.Dispose();

            if (connections == 0)
                Delegate.OnClose(this);
        }

        void SocketClosed()
        {
            connections--;
            if (closed && connections == 0)
            {
                Delegate.OnClose(this);
            }
        }
    }
}
