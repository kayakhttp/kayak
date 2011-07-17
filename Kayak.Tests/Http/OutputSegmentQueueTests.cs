using System;
using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using Kayak.Http;
using System.Linq;

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

        [TearDown]
        public void TearDown()
        {
            Assert.That(mockSocket.GotDispose, Is.False, "Output queue should not dispose socket.");
        }

        IDataProducer SyncProducer(params string[] data)
        {
            var result = new MockDataProducer(c =>
            {
                foreach (var d in data)
                    c.OnData(new ArraySegment<byte>(Encoding.ASCII.GetBytes(d)), null);

                c.OnEnd();
                return null;
            });
            mockProducers.Add(result);
            return result;
        }

        void RunAsyncProd(IDataConsumer consumer, IEnumerator<ArraySegment<byte>> data)
        {
            while (true)
            {
                if (!data.MoveNext())
                {
                    consumer.OnEnd();
                    data.Dispose();
                    break;
                }

                if (consumer.OnData(data.Current, () => RunAsyncProd(consumer, data)))
                    break;
            }
        }

        IDataProducer AsyncProducer(params string[] data)
        {
            var result = new MockDataProducer(c =>
            {
                RunAsyncProd(c, data.Select(s => new ArraySegment<byte>(Encoding.UTF8.GetBytes(s))).GetEnumerator());
                return null;
            });
            mockProducers.Add(result);
            return result;
        }

        [Test]
        public void completed()
        {
            queue.OnCompleted();

            Assert.That(mockSocket.Buffer.GetString(), Is.Empty);
            Assert.That(mockSocket.GotEnd, Is.True);
        }

        [Test]
        public void error()
        {
            queue.OnError(new Exception("err..."));

            Assert.That(mockSocket.Buffer.GetString(), Is.Empty);
            Assert.That(mockSocket.GotEnd, Is.True);
        }

        [Test]
        public void sync_data__completed()
        {
            queue.OnNext(SyncProducer("[1]", "[2]"));
            Assert.That(mockSocket.Buffer.GetString(), Is.EqualTo("[1][2]"));

            queue.OnCompleted();
            Assert.That(mockSocket.GotEnd, Is.True);
        }

        [Test]
        public void sync_data2x__completed()
        {
            queue.OnNext(SyncProducer("[1]", "[2]"));
            Assert.That(mockSocket.Buffer.GetString(), Is.EqualTo("[1][2]"));

            queue.OnNext(SyncProducer("[3]", "[4]"));
            Assert.That(mockSocket.Buffer.GetString(), Is.EqualTo("[1][2][3][4]"));

            queue.OnCompleted();
            Assert.That(mockSocket.GotEnd, Is.True);
        }

        [Test]
        public void async_data__completed()
        {
            queue.OnNext(AsyncProducer("[1]", "[2]"));
            Assert.That(mockSocket.Buffer.GetString(), Is.EqualTo("[1][2]"));

            queue.OnCompleted();
            Assert.That(mockSocket.GotEnd, Is.True);
        }

        [Test]
        public void async_data2x__completed()
        {
            queue.OnNext(AsyncProducer("[1]", "[2]"));
            Assert.That(mockSocket.Buffer.GetString(), Is.EqualTo("[1][2]"));

            queue.OnNext(AsyncProducer("[3]", "[4]"));
            Assert.That(mockSocket.Buffer.GetString(), Is.EqualTo("[1][2][3][4]"));

            queue.OnCompleted();
            Assert.That(mockSocket.GotEnd, Is.True);
        }

        [Test]
        public void sync_data__error()
        {
            queue.OnNext(SyncProducer("[1]", "[2]"));
            Assert.That(mockSocket.Buffer.GetString(), Is.EqualTo("[1][2]"));

            queue.OnError(new Exception("oops"));
            Assert.That(mockSocket.GotEnd, Is.True);
        }

        [Test]
        public void async_data__error()
        {
            queue.OnNext(AsyncProducer("[1]", "[2]"));
            Assert.That(mockSocket.Buffer.GetString(), Is.EqualTo("[1][2]"));

            queue.OnError(new Exception("oops"));
            Assert.That(mockSocket.GotEnd, Is.True);
        }
    }
}
