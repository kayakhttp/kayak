using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Text;
using System.Threading;
using Kayak;
using NUnit.Framework;

namespace KayakTests.Net
{
    class NetTests
    {
        IScheduler scheduler;
        IServer server;
        ServerDelegate serverDelegate;
        ISocket client;
        SocketDelegate clientSocketDelegate;
        SocketDelegate serverSocketDelegate;
        EventContext context;
        IPEndPoint ep;
        
        [SetUp]
        public void SetUp()
        {
            ep = new IPEndPoint(IPAddress.Loopback, Config.Port);
            scheduler = new KayakScheduler();
            server = new KayakServer(scheduler);
            serverDelegate = new ServerDelegate(server);
            client = new KayakSocket(scheduler);
            clientSocketDelegate = new SocketDelegate(client);
            context = new EventContext(scheduler);
        }

        [TearDown]
        public void TearDown()
        {
            serverDelegate.Dispose();
            server.Dispose();
            context.Dispose();
            clientSocketDelegate.Dispose();
            client.Dispose();
        }


        [Test]
        public void Simple_handshake_client_closes_connection()
        {
            serverDelegate.OnConnection = s =>
            {
                Debug.WriteLine("server OnConnection");
                serverSocketDelegate = new SocketDelegate(s);

                serverSocketDelegate.OnEnd = () =>
                {
                    Debug.WriteLine("serverSocket OnEnd");
                    s.End();
                };

                serverSocketDelegate.OnClose = () =>
                {
                    Debug.WriteLine("serverSocket OnClose");
                    s.Dispose();
                };
            };

            context.OnStarted = () =>
            {
                clientSocketDelegate.OnConnected = () =>
                {
                    Debug.WriteLine("client End");
                    client.End();
                };
                clientSocketDelegate.OnClose = () =>
                {
                    Debug.WriteLine("client OnClose");
                    server.Close();
                    scheduler.Stop();
                };

                client.Connect(ep);
            };

            ListenAndRun();

            AssertConnectionAndCleanShutdown();
        }

        [Test]
        public void Client_writes_synchronously_server_buffers_synchronously()
        {
            serverDelegate.OnConnection = s =>
            {
                Debug.WriteLine("server OnConnection");
                serverSocketDelegate = new SocketDelegate(s);

                serverSocketDelegate.OnEnd = () =>
                {
                    Debug.WriteLine("serverSocket OnEnd");
                    s.End();
                };

                serverSocketDelegate.OnClose = () =>
                {
                    Debug.WriteLine("serverSocket OnClose");
                    s.Dispose();
                };
            };

            context.OnStarted = () =>
            {
                clientSocketDelegate.OnConnected = () =>
                {
                    Debug.WriteLine("client OnConnected");
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
                    Debug.WriteLine("client OnClose");
                    server.Close();
                    scheduler.Stop();

                };

                client.Connect(ep);
            };

            ListenAndRun();

            AssertConnectionAndCleanShutdown();
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
                };
            };

            context.OnStarted = () =>
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
                    server.Close();
                    scheduler.Stop();
                };

                client.Connect(ep);
            };

            ListenAndRun();

            AssertConnectionAndCleanShutdown();
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
                    scheduler.Stop();
                };
            };

            context.OnStarted = () =>
            {
                clientSocketDelegate.OnEnd = () =>
                {
                    client.End();
                };

                client.Connect(ep);
            };

            ListenAndRun();

            AssertConnectionAndCleanShutdown();
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
                    scheduler.Stop();
                };
            };

            context.OnStarted = () =>
            {
                clientSocketDelegate.OnEnd = () =>
                {
                    client.End();
                };

                client.Connect(ep);
            };

            ListenAndRun();

            AssertConnectionAndCleanShutdown();
            Assert.That(clientSocketDelegate.GetBufferAsString(),
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

        void ListenAndRun()
        {
            server.Listen(ep);
            context.Run();
        }

        void AssertConnectionAndCleanShutdown()
        {
            Assert.That(clientSocketDelegate.Exception, Is.Null, "Client got error.");
            Assert.That(serverDelegate.NumOnConnectionEvents, Is.EqualTo(1), "Server did not get OnConnection.");
            Assert.That(clientSocketDelegate.NumOnConnectedEvents, Is.EqualTo(1), "Client did not connect.");
            Assert.That(serverSocketDelegate.NumOnEndEvents, Is.EqualTo(1), "Server did not get OnEnd.");
            Assert.That(serverSocketDelegate.NumOnCloseEvents, Is.EqualTo(1), "Server did not get OnClose.");
            Assert.That(clientSocketDelegate.NumOnEndEvents, Is.EqualTo(1), "Client did not get OnEnd.");
            Assert.That(clientSocketDelegate.NumOnCloseEvents, Is.EqualTo(1), "Client did not get OnClose.");
            Assert.That(serverDelegate.NumOnCloseEvents, Is.EqualTo(1), "Server did not raise OnClose.");
        }

        IEnumerable<byte[]> MakeData()
        {
            yield return Encoding.UTF8.GetBytes("hailey is a stinky ");
            yield return Encoding.UTF8.GetBytes("punky butt ");
            yield return Encoding.UTF8.GetBytes("nugget dot com");
        }
    }
}
