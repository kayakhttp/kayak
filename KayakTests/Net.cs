using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Kayak;
using System.Net;
using System.Threading;
using System.Diagnostics;

namespace KayakTests
{
    class NetTests
    {
        public IServer CreateServer()
        {
            return new KayakServer();
        }

        public ISocket CreateSocket()
        {
            return new KayakSocket();
        }

        public IPEndPoint LocalEP(int port)
        {
            return new IPEndPoint(IPAddress.Loopback, port);
        }

        [Test]
        public void Listen_end_point_is_correct_after_binding_and_closing()
        {
            var server1 = CreateServer();
            var ep1 = LocalEP(Config.Port);

            Assert.That(server1.ListenEndPoint, Is.Null);
            server1.Listen(ep1);
            Assert.That(server1.ListenEndPoint, Is.SameAs(ep1));

            var server2 = CreateServer();
            var ep2 = LocalEP(Config.Port + 1);


            Assert.That(server2.ListenEndPoint, Is.Null);
            server2.Listen(ep2);
            Assert.That(server1.ListenEndPoint, Is.SameAs(ep1));

            server1.Close();
            Assert.That(server1.ListenEndPoint, Is.Null);

            server2.Close();
            Assert.That(server1.ListenEndPoint, Is.Null);
        }

        [Test]
        public void Close_before_listen_throws_exception()
        {
            Exception e = null;
            using (var server = CreateServer())
            {
                try
                {
                    Debug.WriteLine("ASDF");
                    server.Close();
                }
                catch (Exception ex)
                {
                    e = ex;
                }
            }

            Assert.That(e, Is.Not.Null);
            Assert.That(e.GetType(), Is.EqualTo(typeof(InvalidOperationException)));
            Assert.That(e.Message, Is.EqualTo("Not listening."));
        }

        [Test]
        public void Double_close_throws_exception()
        {
            Exception e = null;

            using (var server = CreateServer())
            {
                server.Listen(LocalEP(Config.Port));
                server.Close();

                try
                {
                    server.Close();
                }
                catch (Exception ex)
                {
                    e = ex;
                }
            }

            Assert.That(e, Is.Not.Null);
            Assert.That(e.GetType(), Is.EqualTo(typeof(InvalidOperationException)));
            Assert.That(e.Message, Is.EqualTo("Not listening."));
        }

        [Test]
        public void Double_listen_throws_exception()
        {
            Exception e = null;

            using (var server = CreateServer())
            {
                server.Listen(LocalEP(Config.Port));

                try
                {
                    server.Listen(LocalEP(Config.Port));
                }
                catch (Exception ex)
                {
                    e = ex;
                }

                server.Close();
            }

            Assert.That(e, Is.Not.Null);
            Assert.That(e.GetType(), Is.EqualTo(typeof(InvalidOperationException)));
            Assert.That(e.Message, Is.EqualTo("Already listening."));
        }

        [Test]
        public void Accepts_connection()
        {
            bool serverGotConnection = false;
            bool clientGotConnected = false;
            bool clientGotEnd = false;
            Exception clientError = null;
            using (var server = CreateServer())
            {
                var serverDelegate = new ServerDelegate(server);
                serverDelegate.OnConnection = s =>
                    {
                        serverGotConnection = true;

                        var socketDelegate = new SocketDelegate(s);

                        socketDelegate.OnEnd = () =>
                            {
                                s.End();
                            };
                        socketDelegate.OnClose = () =>
                            {
                                s.Dispose();
                            };
                    };

                var ep = LocalEP(Config.Port);
                server.Listen(ep);

                var wh = new ManualResetEventSlim(false);

                var schedulerDelegate = new SchedulerDelegate(KayakScheduler.Current);
                schedulerDelegate.OnStarted = () =>
                    {
                        var socket = CreateSocket();

                        var socketDelegate = new SocketDelegate(socket);
                        socketDelegate.OnConnected = () =>
                        {
                            Debug.WriteLine("Client connected.");
                            clientGotConnected = true;
                            socket.End();
                        };
                        socketDelegate.OnEnd = () =>
                        {
                            Debug.WriteLine("Client got end.");
                            clientGotEnd = true;
                        };
                        socketDelegate.OnError = e =>
                        {
                            Debug.WriteLine("Client error.");
                            clientError = e;
                            socket.Dispose();
                            KayakScheduler.Current.Stop();
                        };
                        socketDelegate.OnClose = () =>
                        {
                            Debug.WriteLine("Client close.");
                            socket.Dispose();

                            Debug.WriteLine("Client disposed.");
                            KayakScheduler.Current.Stop();
                        };

                        Debug.WriteLine("Connecting...");
                        socket.Connect(ep);
                        Debug.WriteLine("???:");
                    };

                schedulerDelegate.OnStopped = () => wh.Set();

                KayakScheduler.Current.Start();

                wh.Wait();
            }

            Assert.That(clientError, Is.Null);
            Assert.That(clientGotEnd);
            Assert.That(clientGotConnected);
            Assert.That(serverGotConnection);
        }
    }

    class SchedulerDelegate : IDisposable
    {
        IScheduler scheduler;

        public Action OnStarted;
        public Action OnStopped;

        public SchedulerDelegate(IScheduler scheduler)
        {
            this.scheduler = scheduler;
            scheduler.OnStarted += new EventHandler(scheduler_OnStarted);
            scheduler.OnStopped += new EventHandler(scheduler_OnStopped);
        }

        public void Dispose()
        {
            scheduler.OnStarted -= new EventHandler(scheduler_OnStarted);
            scheduler.OnStopped -= new EventHandler(scheduler_OnStopped);
            this.scheduler = null;
        }

        void scheduler_OnStopped(object sender, EventArgs e)
        {
            if (OnStopped != null)
                OnStopped();
        }

        void scheduler_OnStarted(object sender, EventArgs e)
        {
            if (OnStarted != null)
                OnStarted();
        }
    }

    class ServerDelegate : IDisposable
    {
        IServer server;
        public Action<ISocket> OnConnection;
        public Action OnClose;

        public ServerDelegate(IServer server)
        {
            this.server = server;
            server.OnConnection += new EventHandler<ConnectionEventArgs>(server_OnConnection);
            server.OnClose += new EventHandler(server_OnClose);
        }

        public void Dispose()
        {
            server.OnConnection -= server_OnConnection;
            server.OnClose -= server_OnClose;
            server = null;
        }

        void server_OnConnection(object sender, ConnectionEventArgs e)
        {
            if (OnConnection != null)
            {
                OnConnection(e.Socket);
            }
            else e.Socket.Dispose();
        }

        void server_OnClose(object sender, EventArgs e)
        {
            if (OnClose != null)
                OnClose();
        }
    }

    class SocketDelegate : IDisposable
    {
        ISocket socket;

        public Action OnTimeout;
        public Action<Exception> OnError;
        public Action OnEnd;
        public Func<ArraySegment<byte>, Action, bool> OnData;
        public Action OnConnected;
        public Action OnClose;

        public SocketDelegate(ISocket socket)
        {
            this.socket = socket;
            socket.OnClose += socket_OnClose;
            socket.OnConnected += socket_OnConnected;
            socket.OnData += socket_OnData;
            socket.OnEnd += socket_OnEnd;
            socket.OnError += socket_OnError;
            socket.OnTimeout += socket_OnTimeout;
        }

        public void Dispose()
        {
            socket.OnClose -= socket_OnClose;
            socket.OnConnected -= socket_OnConnected;
            socket.OnData -= socket_OnData;
            socket.OnEnd -= socket_OnEnd;
            socket.OnError -= socket_OnError;
            socket.OnTimeout -= socket_OnTimeout;
            socket = null;
        }

        void socket_OnTimeout(object sender, EventArgs e)
        {
            if (OnTimeout != null)
                OnTimeout();
        }

        void socket_OnError(object sender, ExceptionEventArgs e)
        {
            if (OnError != null)
                OnError(e.Exception);
        }

        void socket_OnEnd(object sender, EventArgs e)
        {
            if (OnEnd != null)
                OnEnd();
        }

        void socket_OnData(object sender, DataEventArgs e)
        {
            e.WillInvokeContinuation = false;

            if (OnData != null)
                e.WillInvokeContinuation = OnData(e.Data, e.Continuation);
        }

        void socket_OnConnected(object sender, EventArgs e)
        {
            if (OnConnected != null)
                OnConnected();
        }

        void socket_OnClose(object sender, EventArgs e)
        {
            if (OnClose != null)
                OnClose();
        }
    }
}
