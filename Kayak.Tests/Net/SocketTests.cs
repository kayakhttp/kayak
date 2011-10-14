using System;
using System.Diagnostics;
using System.Net;
using System.Text;
using System.Threading;
using NUnit.Framework;

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
		IDisposable stopListening;

        ManualResetEventSlim wh;
        Action schedulerStartedAction;

        Exception schedulerError;

        [SetUp]
        public void SetUp()
        {
            ep = new IPEndPoint(IPAddress.Loopback, Config.Port);

            wh = new ManualResetEventSlim();

            var schedulerDelegate = new SchedulerDelegate();
            scheduler = new DefaultKayakScheduler(schedulerDelegate);

            schedulerDelegate.OnStoppedAction = () =>
            {
                stopListening.Dispose();
                stopListening = null;
                wh.Set();
            };
            schedulerDelegate.OnExceptionAction = e =>
            {
                schedulerError = e;
                Debug.WriteLine("Error on scheduler");
                e.DebugStackTrace();
                scheduler.Stop();
            };

            scheduler.Post(() =>
            {
                stopListening = server.Listen(ep);
                schedulerStartedAction();
            });

            var serverDelegate = new ServerDelegate();
            server = new DefaultKayakServer(serverDelegate, scheduler);

            clientSocketDelegate = new SocketDelegate();
            client = new DefaultKayakSocket(clientSocketDelegate, scheduler);
        }

        [TearDown]
        public void TearDown()
        {
			if (stopListening != null)
				stopListening.Dispose();
			
            wh.Dispose();
            client.Dispose();
        }

        void RunScheduler()
        {
            new Thread(() => scheduler.Start()).Start();
            wh.Wait(1000);
            Assert.That(schedulerError, Is.Null);
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

        [Test]
        public void Constructor_sets_RemoteEndPoint()
        {
            var socketWrapper = new MockSocketWrapper();
            var ip = new IPEndPoint(IPAddress.Parse("9.9.9.9"), 40);

            socketWrapper.RemoteEndPoint = ip;
            var socket = new DefaultKayakSocket(socketWrapper, null);

            Assert.That(socket.RemoteEndPoint, Is.SameAs(ip));
        }

        [Test]
        public void RemoteEndPoint_is_set_after_connected()
        {
            IPEndPoint afterConnect = null;
            IPEndPoint afterConnected = null;
            bool connected = false;

            schedulerStartedAction = () =>
            {
                client.Connect(ep);
                afterConnect = client.RemoteEndPoint;
                clientSocketDelegate.OnConnectedAction = () =>
                {
                    connected = true;
                    afterConnected = client.RemoteEndPoint;
                    scheduler.Stop();
                };
            };

            RunScheduler();

            Assert.That(connected, Is.True);
            Assert.That(afterConnect, Is.EqualTo(ep));
            Assert.That(afterConnected, Is.EqualTo(ep));
        }
    }

    class MockSocketWrapper : ISocketWrapper
    {
        public IPEndPoint RemoteEndPoint
        {
            get;
            set;
        }

        public IAsyncResult BeginConnect(IPEndPoint ep, AsyncCallback callback)
        {
            throw new NotImplementedException();
        }

        public void EndConnect(IAsyncResult iasr)
        {
            throw new NotImplementedException();
        }

        public IAsyncResult BeginReceive(byte[] buffer, int offset, int count, AsyncCallback callback)
        {
            throw new NotImplementedException();
        }

        public int EndReceive(IAsyncResult iasr)
        {
            throw new NotImplementedException();
        }

        public IAsyncResult BeginSend(System.Collections.Generic.List<ArraySegment<byte>> data, AsyncCallback callback)
        {
            throw new NotImplementedException();
        }

        public int EndSend(IAsyncResult iasr)
        {
            throw new NotImplementedException();
        }

        public void Shutdown()
        {
            throw new NotImplementedException();
        }

        public void Dispose()
        {
            throw new NotImplementedException();
        }
    }
}
