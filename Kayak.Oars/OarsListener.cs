using System;
using System.Collections.Generic;
using System.Text;
using System.Net;
using Oars.Core;
using System.Threading;
using System.IO;
using System.Disposables;
using System.Runtime.InteropServices;
using Kayak.Oars;

namespace Kayak.Oars
{
    public class OarsListener : IObservable<ISocket>, IDisposable
    {
        public IPEndPoint ListenEndPoint { get; private set; }
        public event EventHandler Starting, Started, Stopping, Stopped;

        Thread dispatch;

        EventBase eventBase;
        EVConnListener listener;
        EVEvent exitTimer, queueTimer;
        IObserver<ISocket> observer;
        List<Action> queued;
        bool queueTimerAdded;

        short backlog;
        bool running, stopping;

        public OarsListener(IPEndPoint listenEndPoint, short backlog)
        {
            ListenEndPoint = listenEndPoint;
            this.backlog = backlog;
        }

        #region IObservable<ISocket> Members

        public IDisposable Subscribe(IObserver<ISocket> observer)
        {
            this.observer = observer;
            return Disposable.Create(() => this.observer = null);
        }

        #endregion

        #region IDisposable Members

        public void Dispose()
        {
            if (exitTimer != null)
                exitTimer.Dispose();

            if (listener != null)
                listener.Dispose();

            eventBase.Dispose();
        }

        #endregion

        public void Start()
        {
            dispatch = new Thread(new ThreadStart(Dispatch));
            dispatch.Name = "OarsDispatch";
            dispatch.Start();
        }

        public void Stop()
        {
            if (!running) throw new Exception("not running");
            if (stopping) throw new Exception("already stopping");

            stopping = true;
        }

        void Dispatch()
        {
            if (Starting != null)
                Starting(this, EventArgs.Empty);

            eventBase = new EventBase();

            //queueTimer = EVEvent.CreateTimer(eventBase, false);
            //queueTimer.Activated += QueueTimerActivated;

            listener = new EVConnListener(eventBase, ListenEndPoint, backlog);
            listener.ConnectionAccepted += ListenerConnectionAccepted;

            // something of a hack because we don't want to enable locking.
            exitTimer = EVEvent.CreateTimer(eventBase, true);
            exitTimer.Add(TimeSpan.FromSeconds(1));
            exitTimer.Activated += ExitTimerActivated;

            running = true;

            if (Started != null)
                Started(this, EventArgs.Empty);

            eventBase.Dispatch();

            exitTimer.Activated -= ExitTimerActivated;
            exitTimer.Dispose();
            exitTimer = null;

            listener.ConnectionAccepted -= ListenerConnectionAccepted;
            listener.Dispose();
            listener = null;

            eventBase.Dispatch();
            eventBase = null;

            if (Stopped != null)
                Stopped(this, EventArgs.Empty);
        }

        void ListenerConnectionAccepted(object sender, ConnectionAcceptedEventArgs e)
        {
            //Trace.Write("Accepted connection.");
            observer.OnNext(new OarsSocket(eventBase, e.Socket, e.RemoteEndPoint));
        }

        void ExitTimerActivated(object sender, EventArgs e)
        {
            //Console.WriteLine("Exit timer activated.");
            if (stopping)
            {
                if (Stopping != null)
                    Stopping(this, EventArgs.Empty);

                exitTimer.Delete();
                listener.Disable();
            }
        }

        //void QueueTimerActivated(object sender, EventArgs e)
        //{
        //    Trace.Write(" ---------- BEGIN QueueTimerActivated ---------- ");
        //    queueTimer.Delete();
        //    queueTimerAdded = false;


        //    var q = queued;
        //    queued = null;
        //    foreach (var action in q)
        //        action();

        //    Trace.Write(" ---------- END QueueTimerActivated ---------- ");
        //}
    }
}
