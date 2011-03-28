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
    // TODO
    // - ISocket.Close is always dispatched (i.e., after error)
    // - exceptions thrown on scheduler are handled in sane/predictable way.
    class NetTests
    {
        IScheduler scheduler;
        SchedulerDelegate schedulerDelegate;
        IServer server;
        ServerDelegate serverDelegate;
        ISocket client;
        SocketDelegate clientDelegate;
        
        [SetUp]
        public void SetUp()
        {
            scheduler = new KayakScheduler();
            schedulerDelegate = new SchedulerDelegate(scheduler);
            server = new KayakServer(scheduler);
            serverDelegate = new ServerDelegate(server);
            client = new KayakSocket(scheduler);
            clientDelegate = new SocketDelegate(client);
        }

        [TearDown]
        public void TearDown()
        {
            serverDelegate.Dispose();
            server.Dispose();
            schedulerDelegate.Dispose();
            clientDelegate.Dispose();
            client.Dispose();
        }

        public IPEndPoint LocalEP(int port)
        {
            return new IPEndPoint(IPAddress.Loopback, port);
        }

        [Test]
        public void Listen_end_point_is_correct_after_binding_and_closing()
        {
            var ep1 = LocalEP(Config.Port);

            Assert.That(server.ListenEndPoint, Is.Null);

            server.Listen(ep1);
            Assert.That(server.ListenEndPoint, Is.SameAs(ep1));

            var ep2 = LocalEP(Config.Port + 1);

            server.Close();
            Assert.That(server.ListenEndPoint, Is.Null);

            server.Listen(ep2);
            Assert.That(server.ListenEndPoint, Is.SameAs(ep2));

            server.Close();
            Assert.That(server.ListenEndPoint, Is.Null);
        }

        [Test]
        public void Close_before_listen_throws_exception()
        {
            Exception e = null;
               
            try
            {
                server.Close();
            }
            catch (Exception ex)
            {
                e = ex;
            }

            Assert.That(e, Is.Not.Null);
            Assert.That(e.GetType(), Is.EqualTo(typeof(InvalidOperationException)));
            Assert.That(e.Message, Is.EqualTo("Not listening."));
        }

        [Test]
        public void Double_close_throws_exception()
        {
            Exception e = null;

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

            Assert.That(e, Is.Not.Null);
            Assert.That(e.GetType(), Is.EqualTo(typeof(InvalidOperationException)));
            Assert.That(e.Message, Is.EqualTo("Not listening."));
        }

        [Test]
        public void Double_listen_throws_exception()
        {
            Exception e = null;

            var ep = LocalEP(Config.Port);
            Assert.That(server.ListenEndPoint, Is.Null);
            server.Listen(ep);
            Assert.That(server.ListenEndPoint, Is.SameAs(ep));

            try
            {
                server.Listen(LocalEP(Config.Port));
            }
            catch (Exception ex)
            {
                e = ex;
            }

            server.Close();

            Assert.That(e, Is.Not.Null);
            Assert.That(e.GetType(), Is.EqualTo(typeof(InvalidOperationException)));
            Assert.That(e.Message, Is.EqualTo("Already listening."));
        }

        [Test]
        public void Simple_handshake_client_closes_connection()
        {
            bool serverGotConnection = false;
            bool serverGotEnd = false;
            bool serverGotClose = false;
            bool clientGotConnected = false;
            bool clientGotEnd = false;
            bool clientGotClose = false;

            Exception clientError = null;
            bool serverDidClose = false;

            serverDelegate.OnConnection = s =>
            {
                serverGotConnection = true;

                var socketDelegate = new SocketDelegate(s);

                socketDelegate.OnEnd = () =>
                {
                    serverGotEnd = true;
                    s.End();
                };
                socketDelegate.OnClose = () =>
                {
                    serverGotClose = true;
                    s.Dispose();
                    server.Close();
                };
            };
            serverDelegate.OnClose = () =>
            {
                serverDidClose = true;
            };

            var ep = LocalEP(Config.Port);
            var wh = new ManualResetEventSlim(false);

            schedulerDelegate.OnStarted = () =>
            {
                clientDelegate.OnConnected = () =>
                {
                    clientGotConnected = true;
                    client.End();
                };
                clientDelegate.OnEnd = () =>
                {
                    clientGotEnd = true;
                };
                clientDelegate.OnError = e =>
                {
                    clientError = e;
                };
                clientDelegate.OnClose = () =>
                {
                    clientGotClose = true;
                    KayakScheduler.Current.Stop();
                };

                client.Connect(ep);
            };

            schedulerDelegate.OnStopped = () => wh.Set();

            server.Listen(ep);
            scheduler.Start();

            wh.Wait();

            Assert.That(clientError, Is.Null);
            Assert.That(serverGotConnection);
            Assert.That(clientGotConnected);
            Assert.That(serverGotEnd);
            Assert.That(serverGotClose);
            Assert.That(serverDidClose);
            Assert.That(clientGotEnd);
            Assert.That(clientGotClose);
        }


        IEnumerable<byte[]> MakeData()
        {
            yield return Encoding.UTF8.GetBytes("hailey is a stinky ");
            yield return Encoding.UTF8.GetBytes("punky butt ");
            yield return Encoding.UTF8.GetBytes("nugget dot com");
        }

        [Test]
        public void Client_writes_synchronously_server_buffers_synchronously()
        {
            bool serverGotConnection = false;
            bool serverGotEnd = false;
            bool serverGotClose = false;
            bool clientGotEnd = false;
            bool clientGotClose = false;
            bool clientGotConnected = false;
            bool serverDidClose = false;

            Exception clientError = null;

            List<byte[]> buffer = new List<byte[]>();

            Action<ArraySegment<byte>> addToBuffer = d =>
            {
                byte[] b = new byte[d.Count];
                Buffer.BlockCopy(d.Array, d.Offset, b, 0, d.Count);
                buffer.Add(b);
            };

            serverDelegate.OnConnection = s =>
            {
                serverGotConnection = true;

                var socketDelegate = new SocketDelegate(s);
                socketDelegate.OnData = (d, c) =>
                {
                    addToBuffer(d);
                    return false;
                };

                socketDelegate.OnEnd = () =>
                {
                    serverGotEnd = true;
                    s.End();
                };

                socketDelegate.OnClose = () =>
                {
                    serverGotClose = true;
                    s.Dispose();
                    server.Close();
                };
            };
            serverDelegate.OnClose = () =>
            {
                serverDidClose = true;
            };

            var ep = LocalEP(Config.Port);
            var wh = new ManualResetEventSlim(false);

            schedulerDelegate.OnStarted = () =>
            {
                clientDelegate.OnConnected = () =>
                {
                    Debug.WriteLine("will write some datums.");
                    clientGotConnected = true;
                    try
                    {
                        foreach (var d in MakeData())
                            client.Write(new ArraySegment<byte>(d), null);
                        client.End();
                    }
                    catch (Exception e)
                    {
                        Debug.WriteLine("Error while writing to socket." + e.Message);
                        e.PrintStacktrace();
                    }
                };

                clientDelegate.OnEnd = () =>
                {
                    clientGotEnd = true;
                };

                clientDelegate.OnClose = () =>
                {
                    clientGotClose = true;
                    KayakScheduler.Current.Stop();
                };

                client.Connect(ep);
            };

            schedulerDelegate.OnStopped = () => wh.Set();

            server.Listen(ep);
            scheduler.Start();

            wh.Wait();

            Assert.That(clientError, Is.Null);
            Assert.That(serverGotConnection);
            Assert.That(clientGotConnected);
            Assert.That(serverGotEnd);
            Assert.That(serverGotClose);
            Assert.That(serverDidClose);
            Assert.That(clientGotEnd);
            Assert.That(clientGotClose);
            Assert.That(
                buffer.Aggregate("", (acc, next) => acc + Encoding.UTF8.GetString(next)),
                Is.EqualTo("hailey is a stinky punky butt nugget dot com"));
        }

        void WriteData(IEnumerator<byte[]> ds)
        {
            if (ds.MoveNext())
            {
                if (!client.Write(new ArraySegment<byte>(ds.Current), () => WriteData(ds)))
                    WriteData(ds);
            }
            else
            {
                ds.Dispose();
                client.End();
            }
        }

        [Test]
        public void Client_writes_asynchronously_server_buffers_synchronously()
        {
            bool serverGotConnection = false;
            bool serverGotEnd = false;
            bool serverGotClose = false;
            bool serverDidClose = false;
            bool clientGotEnd = false;
            bool clientGotClose = false;
            bool clientGotConnected = false;

            Exception clientError = null;

            List<byte[]> buffer = new List<byte[]>();

            Action<ArraySegment<byte>> doBuff = d =>
            {
                byte[] b = new byte[d.Count];
                Buffer.BlockCopy(d.Array, d.Offset, b, 0, d.Count);
                buffer.Add(b);
            };

            serverDelegate.OnConnection = s =>
            {
                serverGotConnection = true;
                var socketDelegate = new SocketDelegate(s);
                socketDelegate.OnData = (d, c) =>
                {
                    doBuff(d);
                    return false;
                };

                socketDelegate.OnEnd = () =>
                {
                    serverGotEnd = true;
                    s.End();
                };

                socketDelegate.OnClose = () =>
                {
                    serverGotClose = true;
                    s.Dispose();
                    server.Close();
                };
            };
            serverDelegate.OnClose = () =>
            {
                serverDidClose = true;
            };

            var ep = LocalEP(Config.Port);
            var wh = new ManualResetEventSlim(false);

            schedulerDelegate.OnStarted = () =>
            {
                clientDelegate.OnConnected = () =>
                {
                    Debug.WriteLine("will write some datums.");
                    clientGotConnected = true;
                    try
                    {
                        var en = MakeData().GetEnumerator();
                        WriteData(en);
                    }
                    catch (Exception e)
                    {
                        Debug.WriteLine("Error while writing to socket." + e.Message);
                        e.PrintStacktrace();
                    }
                };

                clientDelegate.OnEnd = () =>
                {
                    clientGotEnd = true;
                };

                clientDelegate.OnClose = () =>
                {
                    clientGotClose = true;
                    KayakScheduler.Current.Stop();
                };

                client.Connect(ep);
            };

            schedulerDelegate.OnStopped = () => wh.Set();

            server.Listen(ep);
            scheduler.Start();

            wh.Wait();

            Assert.That(clientError, Is.Null);
            Assert.That(serverGotConnection);
            Assert.That(clientGotConnected);
            Assert.That(serverGotEnd);
            Assert.That(serverGotClose);
            Assert.That(serverDidClose);
            Assert.That(clientGotEnd);
            Assert.That(clientGotClose);
            Assert.That(
                buffer.Aggregate("", (acc, next) => acc + Encoding.UTF8.GetString(next)),
                Is.EqualTo("hailey is a stinky punky butt nugget dot com"));
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
