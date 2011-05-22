using System;
using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using Kayak.Http;

namespace Kayak.Tests.Http
{
    [TestFixture]
    public class OutputSegmentQueueTests
    {
        MockSocket mockSocket;
        OutputSegmentQueue queue;
        List<MockDataProducer> mockProducers;

        [SetUp]
        public void SetUp()
        {
            mockSocket = new MockSocket();
            queue = new OutputSegmentQueue(mockSocket);
            mockProducers = new List<MockDataProducer>();
        }

        IDataProducer SyncProducer()
        {
            var result = new MockDataProducer(c =>
            {
                c.OnData(new ArraySegment<byte>(Encoding.ASCII.GetBytes("some data")), null);
                c.OnData(new ArraySegment<byte>(Encoding.ASCII.GetBytes("some other data")), null);
                c.OnEnd();
                return null;
            });
            mockProducers.Add(result);
            return result;
        }
        IDataProducer AsyncProducer()
        {
            var result = new MockDataProducer(c =>
            {
                Action secondWrite = () =>
                {
                    if (!c.OnData(new ArraySegment<byte>(Encoding.ASCII.GetBytes("some other data")), () => c.OnEnd()))
                        c.OnEnd();
                };

                if (!c.OnData(new ArraySegment<byte>(Encoding.ASCII.GetBytes("some data")), secondWrite))
                    secondWrite();
                return null;
            });
            mockProducers.Add(result);
            return result;
        }

        [Test]
        public void completed()
        {
            queue.OnCompleted();

            Assert.That(mockSocket.Buffer.ToString(), Is.Empty);
            Assert.That(mockSocket.GotEnd, Is.True);
            Assert.That(mockSocket.GotDispose, Is.True);
        }

        [Test]
        public void error()
        {
            queue.OnError(new Exception("err..."));

            Assert.That(mockSocket.Buffer.ToString(), Is.Empty);
            Assert.That(mockSocket.GotDispose, Is.True);
        }

        [Test]
        public void sync_data__completed()
        {
            queue.OnNext(SyncProducer());
            queue.OnCompleted();

            Assert.That(mockSocket.Buffer.ToString(), Is.EqualTo("some datasome other data"));
            Assert.That(mockSocket.GotEnd, Is.True);
            Assert.That(mockSocket.GotDispose, Is.True);
        }

        [Test]
        public void async_data__completed()
        {
            queue.OnNext(AsyncProducer());
            queue.OnCompleted();

            Assert.That(mockSocket.Buffer.ToString(), Is.EqualTo("some datasome other data"));
            Assert.That(mockSocket.GotEnd, Is.True);
            Assert.That(mockSocket.GotDispose, Is.True);
        }

        [Test]
        public void sync_data__error()
        {
            queue.OnNext(SyncProducer());
            queue.OnError(new Exception("oops"));

            Assert.That(mockSocket.Buffer.ToString(), Is.EqualTo("some datasome other data"));
            Assert.That(mockSocket.GotDispose, Is.True);
        }

        [Test]
        public void async_data__error()
        {
            queue.OnNext(AsyncProducer());
            queue.OnError(new Exception("oops"));

            Assert.That(mockSocket.Buffer.ToString(), Is.EqualTo("some datasome other data"));
            Assert.That(mockSocket.GotDispose, Is.True);
        }
    }
}
