using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Text;
using System.Threading;
using Kayak;
using NUnit.Framework;

namespace Kayak.Tests.Net
{
    class NetTests
    {
        IScheduler scheduler;
        SchedulerDelegate schedulerDelegate;
        IServer server;
        ServerDelegate serverDelegate;
        ISocket client;
        SocketDelegate clientSocketDelegate;
        SocketDelegate serverSocketDelegate;
        //EventContext context;
        IPEndPoint ep;

        ManualResetEventSlim wh;
        Action schedulerStartedAction;

        [SetUp]
        public void SetUp()
        {
            ep = new IPEndPoint(IPAddress.Loopback, Config.Port);

            wh = new ManualResetEventSlim();

            IDisposable d = null;

            schedulerDelegate = new SchedulerDelegate();
            schedulerDelegate.OnStoppedAction = () =>
            {
                d.Dispose();
                wh.Set();
            };

            scheduler = new KayakScheduler(schedulerDelegate);
            scheduler.Post(() =>
            {
                d = server.Listen(ep);
                schedulerStartedAction();
            });

            serverDelegate = new ServerDelegate();
            server = new KayakServer(serverDelegate, scheduler);
            
            clientSocketDelegate = new SocketDelegate();
            client = new KayakSocket(clientSocketDelegate, scheduler);
        }

        [TearDown]
        public void TearDown()
        {
            wh.Dispose();
            server.Dispose();
            client.Dispose();
        }

        void RunScheduler()
        {
            new Thread(() => scheduler.Start()).Start();
            wh.Wait();
        }

        [Test]
        public void Simple_handshake_client_closes_connection()
        {
            serverDelegate.OnConnectionAction = (server, socket) =>
            {
                Debug.WriteLine("server OnConnection");
                serverSocketDelegate = new SocketDelegate();

                serverSocketDelegate.OnEndAction = () =>
                {
                    Debug.WriteLine("serverSocket OnEnd");
                    socket.End();
                };

                serverSocketDelegate.OnCloseAction = () =>
                {
                    Debug.WriteLine("serverSocket OnClose");
                    socket.Dispose();
                };

                return serverSocketDelegate;
            };

            schedulerStartedAction = () =>
            {
                clientSocketDelegate.OnConnectedAction = () =>
                {
                    Debug.WriteLine("client End");
                    client.End();
                };
                clientSocketDelegate.OnCloseAction = () =>
                {
                    Debug.WriteLine("client OnClose");
                    scheduler.Stop();
                };

                client.Connect(ep);
            };

            RunScheduler();

            AssertConnectionAndCleanShutdown();
        }

        [Test]
        public void Client_writes_synchronously_server_buffers_synchronously()
        {
            serverDelegate.OnConnectionAction = (server, socket) =>
            {
                Debug.WriteLine("server OnConnection");
                serverSocketDelegate = new SocketDelegate();

                serverSocketDelegate.OnEndAction = () =>
                {
                    Debug.WriteLine("serverSocket OnEnd");
                    socket.End();
                };

                serverSocketDelegate.OnCloseAction = () =>
                {
                    Debug.WriteLine("serverSocket OnClose");
                    socket.Dispose();
                };

                return serverSocketDelegate;
            };

            schedulerStartedAction = () =>
            {
                clientSocketDelegate.OnConnectedAction = () =>
                {
                    Debug.WriteLine("client OnConnected");
                    try
                    {
                        WriteDataSync(client);
                    }
                    catch (Exception e)
                    {
                        Console.Error.WriteStacktrace(e);
                    }
                };

                clientSocketDelegate.OnCloseAction = () =>
                {
                    Debug.WriteLine("client OnClose");
                    scheduler.Stop();

                };

                client.Connect(ep);
            };

            RunScheduler();

            AssertConnectionAndCleanShutdown();
            Assert.That(
                serverSocketDelegate.Buffer.GetString(),
                Is.EqualTo("hailey is a stinky punky butt nugget dot com"));
        }

        [Test]
        public void Client_writes_asynchronously_server_buffers_synchronously()
        {
            serverDelegate.OnConnectionAction = (server, socket) =>
            {
                serverSocketDelegate = new SocketDelegate();

                serverSocketDelegate.OnEndAction = () =>
                {
                    socket.End();
                };

                serverSocketDelegate.OnCloseAction = () =>
                {
                    socket.Dispose();
                };

                return serverSocketDelegate;
            };

            schedulerStartedAction = () =>
            {
                clientSocketDelegate.OnConnectedAction = () =>
                {
                    try
                    {
                        WriteDataAsync(client);
                    }
                    catch (Exception e)
                    {
                        Console.Error.WriteStacktrace(e);
                    }
                };

                clientSocketDelegate.OnCloseAction = () =>
                {
                    scheduler.Stop();
                };

                client.Connect(ep);
            };

            RunScheduler();

            AssertConnectionAndCleanShutdown();
            Assert.That(serverSocketDelegate.Buffer.GetString(),
                Is.EqualTo("hailey is a stinky punky butt nugget dot com"));
        }

        [Test]
        public void Server_writes_synchronously_client_buffers_synchronously()
        {
            serverDelegate.OnConnectionAction = (server, socket) =>
            {
                serverSocketDelegate = new SocketDelegate();

                serverSocketDelegate.OnCloseAction = () =>
                {
                    socket.Dispose();
                    scheduler.Stop();
                };

                WriteDataSync(socket);

                return serverSocketDelegate;
            };

            schedulerStartedAction = () =>
            {
                clientSocketDelegate.OnEndAction = () =>
                {
                    client.End();
                };

                client.Connect(ep);
            };

            RunScheduler();

            AssertConnectionAndCleanShutdown();
            Assert.That(clientSocketDelegate.Buffer.GetString(),
                Is.EqualTo("hailey is a stinky punky butt nugget dot com"));
        }

        [Test]
        public void Server_writes_asynchronously_client_buffers_synchronously()
        {
            serverDelegate.OnConnectionAction = (server, socket) =>
            {
                serverSocketDelegate = new SocketDelegate();

                serverSocketDelegate.OnCloseAction = () =>
                {
                    Debug.WriteLine("will dispose");
                    socket.Dispose();
                    Debug.WriteLine("did dispose");
                    scheduler.Stop();
                };

                WriteDataSync(socket);

                return serverSocketDelegate;
            };

            schedulerStartedAction = () =>
            {
                clientSocketDelegate.OnEndAction = () =>
                {
                    client.End();
                };

                client.Connect(ep);
            };

            RunScheduler();

            AssertConnectionAndCleanShutdown();
            Assert.That(clientSocketDelegate.Buffer.GetString(),
                Is.EqualTo("hailey is a stinky punky butt nugget dot com"));
        }

        void WriteDataSync(ISocket socket)
        {
            foreach (var d in MakeData())
            {
                Debug.WriteLine("Client writing data sync.");
                socket.Write(new ArraySegment<byte>(d), null);
            }
            Debug.WriteLine("Client ending connection.");
            socket.End();
        }

        void WriteDataAsync(ISocket socket)
        {
            var en = MakeData().GetEnumerator();
            WriteDataAsync(socket, en);
        }

        void WriteDataAsync(ISocket socket, IEnumerator<byte[]> ds)
        {
            if (ds.MoveNext())
            {
                Debug.WriteLine("Client writing data async.");
                if (!socket.Write(new ArraySegment<byte>(ds.Current), () => WriteDataAsync(socket, ds)))
                    WriteDataAsync(socket, ds);
            }
            else
            {
                Debug.WriteLine("Client ending connection.");
                ds.Dispose();
                socket.End();
            }
        }

        void AssertConnectionAndCleanShutdown()
        {
            Assert.That(schedulerDelegate.Exception, Is.Null, "Scheduler got exception.");
            Assert.That(clientSocketDelegate.Exception, Is.Null, "Client got error.");
            Assert.That(serverDelegate.NumOnConnectionEvents, Is.EqualTo(1), "Server did not get OnConnection.");
            Assert.That(clientSocketDelegate.NumOnConnectedEvents, Is.EqualTo(1), "Client did not connect.");
            Assert.That(serverSocketDelegate.GotOnEnd, Is.True, "Server did not get OnEnd.");
            Assert.That(serverSocketDelegate.GotOnClose, Is.True, "Server did not get OnClose.");
            Assert.That(clientSocketDelegate.GotOnEnd, Is.True, "Client did not get OnEnd.");
            Assert.That(clientSocketDelegate.GotOnClose, Is.True, "Client did not get OnClose.");
            Assert.That(serverDelegate.NumOnCloseEvents, Is.EqualTo(1), "Server did not raise OnClose.");
        }

        // XXX pull out to somewhere else
        public static IEnumerable<byte[]> MakeData()
        {
            yield return Encoding.UTF8.GetBytes("hailey is a stinky ");
            yield return Encoding.UTF8.GetBytes("punky butt ");
            yield return Encoding.UTF8.GetBytes("nugget dot com");
        }
    }
}
