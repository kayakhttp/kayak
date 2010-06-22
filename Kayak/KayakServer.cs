using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Oars;
using System.Collections;
using System.Net;
using System.IO;

namespace Kayak
{
    public class KayakServer : IKayakServer
    {
        public object UserData { get; private set; }

        IKayakContextFactory contextFactory;
        ObserverCollection<IKayakContext> observers;

        public KayakServer(IObservable<ISocket> connections) : this(connections, null) { }
        public KayakServer(IObservable<ISocket> connections, object userData) : this(connections, userData, KayakContext.DefaultFactory) { }
        public KayakServer(IObservable<ISocket> connections, object userData, IKayakContextFactory contextFactory)
        {
            UserData = userData;
            this.contextFactory = contextFactory;
            observers = new ObserverCollection<IKayakContext>();
            Main(connections).AsCoroutine().Start();
        }
		
		IEnumerable<object> Main(IObservable<ISocket> connections)
		{
            var nullTerminated = connections.EndWithDefault();
			
			while (true)
			{
				ISocket connection = null;

                //Console.WriteLine("Waiting for connection.");
                yield return nullTerminated.Do(c => connection = c).Take(1);

                if (connection == null)
                {
                    //Console.WriteLine("Got null connection.");
                    break;
                }

                //Console.WriteLine("Got connection.");
                var context = contextFactory.CreateContext(this, connection);
                
                if (context == null) continue;

                // calls our observers.Next when the context generates a unit
                // (i.e., read the request headers). calls observers.Error
                // if the context generates an error. calls
                ContextObserver.Observe(this, context);

                context.Start();
			}

            //Console.WriteLine("Sending completed.");
            observers.Completed();
        }

        public IDisposable Subscribe(IObserver<IKayakContext> observer)
        {
            return observers.Add(observer);
        }

        void ContextCompleted(IKayakContext context)
        {
            context.Socket.Dispose();
        }

        class ContextObserver : IObserver<Unit>
        {
            public static void Observe(KayakServer server, IKayakContext context)
            {
                var o = new ContextObserver(server, context);
                o.Subscribe();
            }

            IKayakContext context;
            KayakServer server;
            IDisposable cx;

            public ContextObserver(KayakServer server, IKayakContext context)
            {
                this.server = server;
                this.context = context;
            }

            public void Subscribe()
            {
                cx = context.Subscribe(this);
            }

            public void OnCompleted()
            {
                server.ContextCompleted(context);
                cx.Dispose();
            }

            public void OnError(Exception exception)
            {
                server.observers.Error(new KayakContextException(context, exception));
                cx.Dispose();
            }

            public void OnNext(Unit value)
            {
                server.observers.Next(context);
            }
        }
    }

    public interface ISocket : IDisposable
    {
        IPEndPoint RemoteEndPoint { get; }
        Stream GetStream();
    }

    public interface IKayakServer : IObservable<IKayakContext>
    {
        object UserData { get; }
    }

    public class KayakContextEventArgs : EventArgs
    {
        public IKayakContext Context { get; set; }

        public KayakContextEventArgs(IKayakContext context)
        {
            Context = context;
        }
    }

    public class KayakContextException : Exception
    {
        public IKayakContext Context { get; private set; }
        public KayakContextException(IKayakContext context, Exception inner) :
            base("An error occurred while processing a context.", inner)
        {
            Context = context;
        }
    }
}
