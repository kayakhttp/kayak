using System;
using System.Net;
using System.Text;
using Kayak;
using Kayak.Tests;
using Kayak.Tests.Net;
using NUnit.Framework;
using System.Threading;

namespace Kayak.Tests.Net
{
    // TODO
    // - ISocket.Close is always dispatched (i.e., after error) (how to test?)
    // - OnData continuation semantics: if handler will invoke, event not fired until continuation
    // - access after dispose throws exception
    // Question mark/internal considerations:
    // - how to test that no data is read until OnData is listened to?
    // - how to test for write behavior when continuation is provided/write returns true?
    class SocketTests
    {
        ISocket client;
        SocketDelegate clientSocketDelegate;
        IScheduler scheduler;
        IServer server;
        IPEndPoint ep;

        ManualResetEventSlim wh;
        Action schedulerStartedAction;

        [SetUp]
        public void SetUp()
        {
            ep = new IPEndPoint(IPAddress.Loopback, Config.Port);

            wh = new ManualResetEventSlim();

            IDisposable d = null;

            var schedulerDelegate = new SchedulerDelegate();
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

            var serverDelegate = new ServerDelegate();
            server = new KayakServer(serverDelegate, scheduler);

            clientSocketDelegate = new SocketDelegate();
            client = new KayakSocket(clientSocketDelegate, scheduler);
        }

        [TearDown]
        public void TearDown()
        {
            wh.Dispose();
            client.Dispose();
        }

        void RunScheduler()
        {
            new Thread(() => scheduler.Start()).Start();
            wh.Wait();
        }

        [Test]
        public void Connect_after_connect_throws_exception()
        {
            Exception ex = null;

            schedulerStartedAction = () =>
            {
                client.Connect(ep);

                try
                {
                    client.Connect(ep);
                }
                catch (InvalidOperationException e)
                {
                    ex = e;
                }

                scheduler.Stop();
            };

            RunScheduler();

            Assert.That(ex, Is.Not.Null);
            Assert.That(ex.Message, Is.EqualTo("The socket was connecting."));
        }

        [Test]
        public void Connect_after_connected_throws_exception()
        {
            Exception ex = null;

            schedulerStartedAction = () =>
            {
                client.Connect(ep);
                clientSocketDelegate.OnConnectedAction = () =>
                {
                    try
                    {
                        client.Connect(ep);
                    }
                    catch (InvalidOperationException e)
                    {
                        ex = e;
                    }
                    scheduler.Stop();
                };
            };

            RunScheduler();

            Assert.That(ex, Is.Not.Null);
            Assert.That(ex.Message, Is.EqualTo("The socket was connected."));
        }


        [Test]
        public void End_before_connect_throws_exception()
        {
            Exception ex = null;

            schedulerStartedAction = () =>
            {
                try
                {
                    client.End();
                }
                catch (InvalidOperationException e)
                {
                    ex = e;
                }
                scheduler.Stop();
            };

            RunScheduler();

            Assert.That(ex, Is.Not.Null);
            Assert.That(ex.Message, Is.EqualTo("The socket was not connected."));
        }

        [Test]
        public void Write_before_connect_throws_exception()
        {
            Exception ex = null;

            schedulerStartedAction = () =>
            {
                try
                {
                    client.Write(default(ArraySegment<byte>), null);
                }
                catch (InvalidOperationException e)
                {
                    ex = e;
                }
                scheduler.Stop();
            };

            RunScheduler();

            Assert.That(ex, Is.Not.Null);
            Assert.That(ex.Message, Is.EqualTo("The socket was not connected."));
        }

        [Test]
        public void End_after_end_throws_exception()
        {
            Exception ex = null;

            schedulerStartedAction = () =>
            {
                client.Connect(ep);
                clientSocketDelegate.OnConnectedAction = () =>
                {
                    client.End();

                    try
                    {
                        client.End();
                    }
                    catch (InvalidOperationException e)
                    {
                        ex = e;
                    }

                    scheduler.Stop();
                };
            };

            RunScheduler();

            Assert.That(ex, Is.Not.Null);
            Assert.That(ex.Message, Is.EqualTo("The socket was previously ended."));
        }

        [Test]
        public void Write_after_end_throws_exception()
        {
            Exception ex = null;

            schedulerStartedAction = () =>
            {
                client.Connect(ep);
                clientSocketDelegate.OnConnectedAction = () =>
                {
                    client.End();

                    try
                    {
                        client.Write(new ArraySegment<byte>(Encoding.UTF8.GetBytes("yo dawg")), null);
                    }
                    catch (InvalidOperationException e)
                    {
                        ex = e;
                    }

                    scheduler.Stop();
                };
            };

            RunScheduler();

            Assert.That(ex, Is.Not.Null);
            Assert.That(ex.Message, Is.EqualTo("The socket was previously ended."));
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

            schedulerStartedAction = () =>
            {
                client.Connect(ep);
                try
                {
                    client.Write(default(ArraySegment<byte>), null);
                }
                catch (InvalidOperationException e)
                {
                    ex = e;
                }
                scheduler.Stop();
            };

            RunScheduler();

            Assert.That(ex, Is.Not.Null);
            Assert.That(ex.Message, Is.EqualTo("The socket was not connected."));
        }


        [Test]
        public void End_before_connected_throws_exception()
        {
            Exception ex = null;

            schedulerStartedAction = () =>
            {
                client.Connect(ep);
                try
                {
                    client.End();
                }
                catch (InvalidOperationException e)
                {
                    ex = e;
                }
                scheduler.Stop();
            };

            RunScheduler();

            Assert.That(ex, Is.Not.Null);
            Assert.That(ex.Message, Is.EqualTo("The socket was not connected."));
        }

        #endregion

        [Test]
        public void Write_with_null_continuation_returns_false()
        {
            bool writeResult = false;
            bool connected = false;

            schedulerStartedAction = () =>
            {
                client.Connect(ep);
                clientSocketDelegate.OnConnectedAction = () =>
                {
                    connected = true;
                    writeResult = client.Write(new ArraySegment<byte>(Encoding.ASCII.GetBytes("hello socket.Write")), null);
                    scheduler.Stop();
                };
            };

            RunScheduler();

            Assert.That(connected, Is.True);
            Assert.That(writeResult, Is.False);
        }

        [Test]
        public void Write_with_zero_length_buffer_returns_false()
        {
            bool writeResult = false;
            bool connected = false;

            schedulerStartedAction = () =>
            {
                client.Connect(ep);
                clientSocketDelegate.OnConnectedAction = () =>
                {
                    connected = true;
                    writeResult = client.Write(default(ArraySegment<byte>), () => { });
                    scheduler.Stop();
                };
            };

            RunScheduler();

            Assert.That(connected, Is.True);
            Assert.That(writeResult, Is.False);
        }
    }
}
