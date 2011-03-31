using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Kayak;
using NUnit.Framework;
using System.Net;

namespace KayakTests.Net
{
    // TODO
    // - ISocket.Close is always dispatched (i.e., after error)
    // - OnData continuation semantics: if handler will invoke, event not fired until continuation
    // - access after dispose throws exception
    // - no data is read until OnData is listened to
    class SocketTests
    {
        EventContext context;
        ISocket client;
        SocketDelegate clientSocketDelegate;
        IScheduler scheduler;
        IServer server;
        IPEndPoint ep;

        [SetUp]
        public void SetUp()
        {
            ep = new IPEndPoint(IPAddress.Loopback, Config.Port);
            scheduler = new KayakScheduler();
            context = new EventContext(scheduler);
            client = new KayakSocket(scheduler);
            clientSocketDelegate = new SocketDelegate(client);
            server = new KayakServer(scheduler);
        }

        void ListenAndRun()
        {
            server.Listen(ep);
            context.Run();
        }

        [TearDown]
        public void TearDown()
        {
            context.Dispose();
            server.Dispose();
            client.Dispose();
            clientSocketDelegate.Dispose();
        }

        [Test]
        public void Connect_after_connect_throws_exception()
        {
            Exception ex = null;

            client.Connect(ep);

            try
            {
                client.Connect(ep);
            }
            catch (Exception e)
            {
                ex = e;
            }

            Assert.That(ex, Is.Not.Null);
            Assert.That(ex.GetType(), Is.EqualTo(typeof(InvalidOperationException)));
            Assert.That(ex.Message, Is.EqualTo("The socket was connecting."));
        }

        [Test]
        public void Connect_after_connected_throws_exception()
        {
            Exception ex = null;

            context.OnStarted = () =>
            {
                client.Connect(ep);
                clientSocketDelegate.OnConnected = () =>
                {
                    try
                    {
                        client.Connect(ep);
                    }
                    catch (Exception e)
                    {
                        ex = e;
                    }
                    scheduler.Stop();
                };
            };

            ListenAndRun();

            Assert.That(ex, Is.Not.Null);
            Assert.That(ex.GetType(), Is.EqualTo(typeof(InvalidOperationException)));
            Assert.That(ex.Message, Is.EqualTo("The socket was connected."));
        }


        [Test]
        public void End_before_connect_throws_exception()
        {
            Exception ex = null;

            try
            {
                client.End();
            }
            catch (Exception e)
            {
                ex = e;
            }

            Assert.That(ex, Is.Not.Null);
            Assert.That(ex.GetType(), Is.EqualTo(typeof(InvalidOperationException)));
            Assert.That(ex.Message, Is.EqualTo("The socket was not connected."));
        }

        [Test]
        public void Write_before_connect_throws_exception()
        {
            Exception ex = null;

            try
            {
                client.Write(default(ArraySegment<byte>), null);
            }
            catch (Exception e)
            {
                ex = e;
            }

            Assert.That(ex, Is.Not.Null);
            Assert.That(ex.GetType(), Is.EqualTo(typeof(InvalidOperationException)));
            Assert.That(ex.Message, Is.EqualTo("The socket was not connected."));
        }

        [Test]
        public void End_after_end_throws_exception()
        {
            Exception ex = null;

            context.OnStarted = () =>
            {
                client.Connect(ep);
                clientSocketDelegate.OnConnected = () =>
                {
                    client.End();

                    try
                    {
                        client.End();
                    }
                    catch (Exception e)
                    {
                        ex = e;
                    }

                    scheduler.Stop();
                };
            };

            ListenAndRun();

            Assert.That(ex, Is.Not.Null);
            Assert.That(ex.GetType(), Is.EqualTo(typeof(InvalidOperationException)));
            Assert.That(ex.Message, Is.EqualTo("The socket was not connected."));
        }

        [Test]
        public void Write_after_end_throws_exception()
        {
            Exception ex = null;

            context.OnStarted = () =>
            {
                client.Connect(ep);
                clientSocketDelegate.OnConnected = () =>
                {
                    client.End();

                    try
                    {
                        client.Write(default(ArraySegment<byte>), null);
                    }
                    catch (Exception e)
                    {
                        ex = e;
                    }

                    scheduler.Stop();
                };
            };

            ListenAndRun();

            Assert.That(ex, Is.Not.Null);
            Assert.That(ex.GetType(), Is.EqualTo(typeof(InvalidOperationException)));
            Assert.That(ex.Message, Is.EqualTo("The socket was not connected."));
        }

        #region Temporary behavior
        // TODO eventually it would probably be nice to support a fire-and-forget use-case:
        // socket.Connect(ep);
        // socket.Write(...);
        // socket.End();

        [Test]
        public void Write_before_connected_throws_exception()
        {
            Exception ex = null;

            context.OnStarted = () =>
            {
                client.Connect(ep);
                try
                {
                    client.Write(default(ArraySegment<byte>), null);
                }
                catch (Exception e)
                {
                    ex = e;
                }
                scheduler.Stop();
            };

            ListenAndRun();

            Assert.That(ex, Is.Not.Null);
            Assert.That(ex.GetType(), Is.EqualTo(typeof(InvalidOperationException)));
            Assert.That(ex.Message, Is.EqualTo("The socket was not connected."));
        }


        [Test]
        public void End_before_connected_throws_exception()
        {
            Exception ex = null;

            context.OnStarted = () =>
            {
                client.Connect(ep);
                try
                {
                    client.End();
                }
                catch (Exception e)
                {
                    ex = e;
                }
                scheduler.Stop();
            };

            ListenAndRun();

            Assert.That(ex, Is.Not.Null);
            Assert.That(ex.GetType(), Is.EqualTo(typeof(InvalidOperationException)));
            Assert.That(ex.Message, Is.EqualTo("The socket was not connected."));
        }

        #endregion
    }
}
