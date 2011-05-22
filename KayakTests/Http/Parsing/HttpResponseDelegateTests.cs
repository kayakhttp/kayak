using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Kayak.Http;
using Kayak;

namespace KayakTests.Http
{
    [TestFixture]
    public class HttpResponseDelegateTests
    {
        HttpResponseDelegate del;
        bool connectionClosed;
        Action connectionClosedAction;
        MockDataConsumer mockConsumer;
        Action producerAbort;

        [SetUp]
        public void SetUp()
        {
            connectionClosed = false;
            connectionClosedAction = () => connectionClosed = true;
            mockConsumer = new MockDataConsumer();
        }

        void WriteSync(IDataConsumer c, string str)
        {
            c.OnData(new ArraySegment<byte>(Encoding.UTF8.GetBytes(str)), null);
        }

        HttpResponseHead Head()
        {
            return new HttpResponseHead()
            {
                Status = "a status",
                Headers = new Dictionary<string, string>()
            };
        }

        [Test]
        public void transport_connected_after_on_response()
        {
            del = new HttpResponseDelegate(false, false, false, connectionClosedAction);
            del.renderer = new MockHeaderRender();

            del.OnResponse(Head(), null);

            Assert.That(connectionClosed, Is.False);

            var abort = del.Connect(mockConsumer);

            Assert.That(mockConsumer.Buffer.ToString(), Is.EqualTo("[headers]"));
            Assert.That(mockConsumer.GotEnd, Is.True);
            Assert.That(connectionClosed, Is.True);
        }

        [Test]
        public void transport_connected_before_on_response()
        {
            del = new HttpResponseDelegate(false, false, false, connectionClosedAction);
            del.renderer = new MockHeaderRender();

            var abort = del.Connect(mockConsumer);
            del.OnResponse(Head(), null);

            Assert.That(mockConsumer.Buffer.ToString(), Is.EqualTo("[headers]"));
            Assert.That(mockConsumer.GotEnd, Is.True);
            Assert.That(connectionClosed, Is.True);
        }

        [Test]
        public void transport_connected_after_on_response__async_producer__begin_producing_after_on_connect()
        {
            del = new HttpResponseDelegate(false, false, false, connectionClosedAction);
            del.renderer = new MockHeaderRender();

            Action startProducing = null;
            del.OnResponse(Head(), MockDataProducer.Create(new string[] {
                "Chunk1",
                "Chunk2",
                "Chunk3"
            }, false, () => { }, s => startProducing = s));

            Assert.That(connectionClosed, Is.False);

            var abort = del.Connect(mockConsumer);
            startProducing();
            mockConsumer.Continuation();
            mockConsumer.Continuation();
            mockConsumer.Continuation();

            AssertConsumer();
        }

        [Test]
        public void transport_connected_after_on_response__sync_producer__begin_producing_after_on_connect()
        {
            del = new HttpResponseDelegate(false, false, false, connectionClosedAction);
            del.renderer = new MockHeaderRender();

            Action startProducing = null;
            del.OnResponse(Head(), MockDataProducer.Create(new string[] {
                "Chunk1",
                "Chunk2",
                "Chunk3"
            }, true, () => { }, s => startProducing = s));

            Assert.That(connectionClosed, Is.False);

            var abort = del.Connect(mockConsumer);
            startProducing();

            AssertConsumer();
        }

        [Test]
        public void transport_connected_before_on_response__async_producer__begin_producing_after_on_request()
        {
            del = new HttpResponseDelegate(false, false, false, connectionClosedAction);
            del.renderer = new MockHeaderRender();

            var abort = del.Connect(mockConsumer);

            Action startProducing = null;
            del.OnResponse(Head(), MockDataProducer.Create(new string[] {
                "Chunk1",
                "Chunk2",
                "Chunk3"
            }, false, () => { }, s => startProducing = s));
            startProducing();
            mockConsumer.Continuation();
            mockConsumer.Continuation();
            mockConsumer.Continuation();

            AssertConsumer();
        }

        [Test]
        public void transport_connected_before_on_response__sync_producer__begin_producing_after_on_request()
        {
            del = new HttpResponseDelegate(false, false, false, connectionClosedAction);
            del.renderer = new MockHeaderRender();

            var abort = del.Connect(mockConsumer);

            Action startProducing = null;
            del.OnResponse(Head(), MockDataProducer.Create(new string[] {
                "Chunk1",
                "Chunk2",
                "Chunk3"
            }, true, () => { }, s => startProducing = s));
            startProducing();

            AssertConsumer();
        }

        [Test]
        public void transport_connected_after_on_response__async_producer__begin_producing_on_connect()
        {
            del = new HttpResponseDelegate(false, false, false, connectionClosedAction);
            del.renderer = new MockHeaderRender();

            del.OnResponse(Head(), MockDataProducer.Create(new string[] {
                "Chunk1",
                "Chunk2",
                "Chunk3"
            }, false, () => { }, s => s()));

            Assert.That(connectionClosed, Is.False);

            var abort = del.Connect(mockConsumer);
            mockConsumer.Continuation();
            mockConsumer.Continuation();
            mockConsumer.Continuation();

            AssertConsumer();
        }

        [Test]
        public void transport_connected_after_on_response__sync_producer__begin_producing_on_connect()
        {
            del = new HttpResponseDelegate(false, false, false, connectionClosedAction);
            del.renderer = new MockHeaderRender();

            del.OnResponse(Head(), MockDataProducer.Create(new string[] {
                "Chunk1",
                "Chunk2",
                "Chunk3"
            }, true, () => { }, s => s()));

            Assert.That(connectionClosed, Is.False);

            var abort = del.Connect(mockConsumer);

            AssertConsumer();
        }

        [Test]
        public void transport_connected_before_on_response__async_producer__begin_producing_on_connect()
        {
            del = new HttpResponseDelegate(false, false, false, connectionClosedAction);
            del.renderer = new MockHeaderRender();

            var abort = del.Connect(mockConsumer);

            del.OnResponse(Head(), MockDataProducer.Create(new string[] {
                "Chunk1",
                "Chunk2",
                "Chunk3"
            }, false, () => { }, s => s()));
            mockConsumer.Continuation();
            mockConsumer.Continuation();
            mockConsumer.Continuation();

            AssertConsumer();
        }

        [Test]
        public void transport_connected_before_on_response__sync_producer__begin_producing_on_connect()
        {
            del = new HttpResponseDelegate(false, false, false, connectionClosedAction);
            del.renderer = new MockHeaderRender();

            var abort = del.Connect(mockConsumer);

            del.OnResponse(Head(), MockDataProducer.Create(new string[] {
                "Chunk1",
                "Chunk2",
                "Chunk3"
            }, true, () => { }, s => s()));

            AssertConsumer();
        }

        void AssertConsumer()
        {
            Assert.That(mockConsumer.Buffer.ToString(), Is.EqualTo("[headers]Chunk1Chunk2Chunk3"));
            Assert.That(mockConsumer.GotEnd, Is.True);
            Assert.That(connectionClosed, Is.True);
        }
    }

    class MockHeaderRender : IHeaderRenderer
    {
        public bool Rendered;

        public void Render(IDataConsumer consumer, HttpResponseHead head)
        {
            if (Rendered) throw new InvalidOperationException("already rendered");
            Rendered = true;
            consumer.OnData(new ArraySegment<byte>(Encoding.ASCII.GetBytes("[headers]")), null);
        }
    }
}
