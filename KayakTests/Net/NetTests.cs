using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Kayak;
using System.Net;
using System.Threading;
using System.Diagnostics;

namespace KayakTests.Net
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
        ManualResetEventSlim wh;
        IPEndPoint ep;
        
        [SetUp]
        public void SetUp()
        {
            wh = new ManualResetEventSlim(false);
            ep = new IPEndPoint(IPAddress.Loopback, Config.Port);
            scheduler = new KayakScheduler();
            schedulerDelegate = new SchedulerDelegate(scheduler);
            schedulerDelegate.OnStopped = () => wh.Set();
            server = new KayakServer(scheduler);
            serverDelegate = new ServerDelegate(server);
            client = new KayakSocket(scheduler);
            clientSocketDelegate = new SocketDelegate(client);
        }

        [TearDown]
        public void TearDown()
        {
            serverDelegate.Dispose();
            server.Dispose();
            schedulerDelegate.Dispose();
            clientSocketDelegate.Dispose();
            client.Dispose();
            wh.Dispose();
        }


        [Test]
        public void Simple_handshake_client_closes_connection()
        {
            serverDelegate.OnConnection = s =>
            {
                serverSocketDelegate = new SocketDelegate(s);

                serverSocketDelegate.OnEnd = () =>
                {
                    s.End();
                };

                serverSocketDelegate.OnClose = () =>
                {
                    s.Dispose();
                    server.Close();
                };
            };

            schedulerDelegate.OnStarted = () =>
            {
                clientSocketDelegate.OnConnected = () =>
                {
                    client.End();
                };
                clientSocketDelegate.OnClose = () =>
                {
                    KayakScheduler.Current.Stop();
                };

                client.Connect(ep);
            };

            RunLoop();

            AssertCleanConnectionAndShutdown();
        }

        [Test]
        public void Client_writes_synchronously_server_buffers_synchronously()
        {
            serverDelegate.OnConnection = s =>
            {
                serverSocketDelegate = new SocketDelegate(s);

                serverSocketDelegate.OnEnd = () =>
                {
                    s.End();
                };

                serverSocketDelegate.OnClose = () =>
                {
                    s.Dispose();
                    server.Close();
                };
            };

            schedulerDelegate.OnStarted = () =>
            {
                clientSocketDelegate.OnConnected = () =>
                {
                    try
                    {
                        WriteDataSync(client);
                    }
                    catch (Exception e)
                    {
                        e.PrintStacktrace();
                    }
                };

                clientSocketDelegate.OnClose = () =>
                {
                    KayakScheduler.Current.Stop();
                };

                client.Connect(ep);
            };

            RunLoop();

            AssertCleanConnectionAndShutdown();
            Assert.That(
                serverSocketDelegate.GetBufferAsString(),
                Is.EqualTo("hailey is a stinky punky butt nugget dot com"));
        }

        [Test]
        public void Client_writes_asynchronously_server_buffers_synchronously()
        {
            serverDelegate.OnConnection = s =>
            {
                serverSocketDelegate = new SocketDelegate(s);

                serverSocketDelegate.OnEnd = () =>
                {
                    s.End();
                };

                serverSocketDelegate.OnClose = () =>
                {
                    s.Dispose();
                    server.Close();
                };
            };

            schedulerDelegate.OnStarted = () =>
            {
                clientSocketDelegate.OnConnected = () =>
                {
                    try
                    {
                        WriteDataAsync(client);
                    }
                    catch (Exception e)
                    {
                        e.PrintStacktrace();
                    }
                };

                clientSocketDelegate.OnClose = () =>
                {
                    KayakScheduler.Current.Stop();
                };

                client.Connect(ep);
            };

            RunLoop();

            AssertCleanConnectionAndShutdown();
            Assert.That(serverSocketDelegate.GetBufferAsString(),
                Is.EqualTo("hailey is a stinky punky butt nugget dot com"));
        }

        [Test]
        public void Server_writes_synchronously_client_buffers_synchronously()
        {
            serverDelegate.OnConnection = s =>
            {
                serverSocketDelegate = new SocketDelegate(s);
                WriteDataSync(s);

                serverSocketDelegate.OnClose = () =>
                {
                    s.Dispose();
                    server.Close();
                    KayakScheduler.Current.Stop();
                };
            };

            schedulerDelegate.OnStarted = () =>
            {
                clientSocketDelegate.OnEnd = () =>
                {
                    client.End();
                };

                client.Connect(ep);
            };

            RunLoop();

            AssertCleanConnectionAndShutdown();
            Assert.That(clientSocketDelegate.GetBufferAsString(),
                Is.EqualTo("hailey is a stinky punky butt nugget dot com"));
        }

        [Test]
        public void Server_writes_asynchronously_client_buffers_synchronously()
        {
            serverDelegate.OnConnection = s =>
            {
                serverSocketDelegate = new SocketDelegate(s);
                WriteDataSync(s);

                serverSocketDelegate.OnClose = () =>
                {
                    Debug.WriteLine("will dispose");
                    s.Dispose();
                    Debug.WriteLine("did dispose");
                    server.Close();
                    KayakScheduler.Current.Stop();
                };
            };

            schedulerDelegate.OnStarted = () =>
            {
                clientSocketDelegate.OnEnd = () =>
                {
                    client.End();
                };

                client.Connect(ep);
            };

            RunLoop();

            AssertCleanConnectionAndShutdown();
            Assert.That(clientSocketDelegate.GetBufferAsString(),
                Is.EqualTo("hailey is a stinky punky butt nugget dot com"));
        }

        void WriteDataSync(ISocket socket)
        {
            foreach (var d in MakeData())
                socket.Write(new ArraySegment<byte>(d), null);
            socket.End();
        }

        void WriteDataAsync(ISocket socket)
        {
            var en = MakeData().GetEnumerator();
            WriteData(socket, en);
        }
        void WriteData(ISocket socket, IEnumerator<byte[]> ds)
        {
            if (ds.MoveNext())
            {
                if (!socket.Write(new ArraySegment<byte>(ds.Current), () => WriteData(socket, ds)))
                    WriteData(socket, ds);
            }
            else
            {
                ds.Dispose();
                socket.End();
            }
        }

        void RunLoop()
        {
            server.Listen(ep);
            scheduler.Start();
            wh.Wait();
        }

        void AssertCleanConnectionAndShutdown()
        {
            Assert.That(clientSocketDelegate.Exception, Is.Null);
            Assert.That(serverDelegate.NumOnConnectionEvents, Is.EqualTo(1));
            Assert.That(clientSocketDelegate.NumOnConnectedEvents, Is.EqualTo(1));
            Assert.That(serverSocketDelegate.NumOnEndEvents, Is.EqualTo(1));
            Assert.That(serverSocketDelegate.NumOnCloseEvents, Is.EqualTo(1));
            Assert.That(clientSocketDelegate.NumOnEndEvents, Is.EqualTo(1));
            Assert.That(clientSocketDelegate.NumOnCloseEvents, Is.EqualTo(1));
            Assert.That(serverDelegate.NumOnCloseEvents, Is.EqualTo(1));
        }

        IEnumerable<byte[]> MakeData()
        {
            yield return Encoding.UTF8.GetBytes("hailey is a stinky ");
            yield return Encoding.UTF8.GetBytes("punky butt ");
            yield return Encoding.UTF8.GetBytes("nugget dot com");
        }
    }
}
