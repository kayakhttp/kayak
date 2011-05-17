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

        IDataProducer Producer(Func<IDataConsumer, Action> p)
        {
            return new MockDataProducer(c => new Disposable(p(c)));
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
        public void Delegate_connected_immediately__sync_producer()
        {
            del = new HttpResponseDelegate(false, false, false, connectionClosedAction);
            del.renderer = new MockHeaderRender();

            var abort = del.Connect(mockConsumer);

            producerAbort = () => { };

            del.OnResponse(Head(), Producer(c =>
            {
                WriteSync(c, "Chunk1");
                WriteSync(c, "Chunk2");
                WriteSync(c, "Chunk3");
                c.OnEnd();
                return producerAbort;
            }));

            Assert.That(mockConsumer.Buffer.ToString(), Is.EqualTo("[headers]Chunk1Chunk2Chunk3"));
            Assert.That(mockConsumer.GotEnd, Is.True);
            Assert.That(connectionClosed, Is.True);
        }

        [Test]
        public void Delegate_connected_later__sync_producer()
        {
            del = new HttpResponseDelegate(false, false, false, connectionClosedAction);
            del.renderer = new MockHeaderRender();

            producerAbort = () => { };

            del.OnResponse(Head(), Producer(c =>
            {
                WriteSync(c, "Chunk1");
                WriteSync(c, "Chunk2");
                WriteSync(c, "Chunk3");
                c.OnEnd();
                return producerAbort;
            }));

            Assert.That(mockConsumer.Buffer.ToString(), Is.Empty);
            Assert.That(mockConsumer.GotEnd, Is.False);
            Assert.That(connectionClosed, Is.False);

            var abort = del.Connect(mockConsumer);

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
