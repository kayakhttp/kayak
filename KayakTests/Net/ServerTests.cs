using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using NUnit.Framework;
using Kayak;

namespace KayakTests.Net
{
    // TODO
    // - closes connection if no OnConnection listeners
    // - raises OnClose after being closed.
    class ServerTests
    {
        IServer server;

        public IPEndPoint LocalEP(int port)
        {
            return new IPEndPoint(IPAddress.Loopback, port);
        }

        [SetUp]
        public void SetUp()
        {
            var scheduler = new KayakScheduler();
            server = new KayakServer(scheduler);
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
        public void Close_after_close_throws_exception()
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
        public void Listen_after_listen_throws_exception()
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
    }
}
