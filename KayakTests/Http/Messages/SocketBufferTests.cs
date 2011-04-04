using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Kayak.Http;
using Kayak;
using KayakTests.Net;
using System.Diagnostics;

namespace KayakTests.Http
{
    [TestFixture]
    public class SocketBufferTests
    {
        SocketBuffer buffer;
        MockSocket socket;
        bool drained;

        [SetUp]
        public void SetUp()
        {
            drained = false;
            buffer = new SocketBuffer(() => drained = true);
            socket = new MockSocket();
        }

        [Test]
        public void Writes_data_received_synchronously_and_ended_before_attached_to_socket()
        {
            WriteDataSync();
            buffer.End();

            Assert.That(drained, Is.False);

            buffer.Attach(socket);

            Assert.That(drained, Is.True);
            Assert.That(socket.Buffer.ToString(), Is.EqualTo(DataString()));

            buffer.Detach(socket);
        }

        [Test]
        public void Writes_data_received_synchronously_and_ended_after_attached_to_socket()
        {
            buffer.Attach(socket);

            Assert.That(drained, Is.False);

            WriteDataSync();
            Assert.That(socket.Buffer.ToString(), Is.EqualTo(DataString()));

            buffer.End();

            Assert.That(drained, Is.True);
            Assert.That(socket.Buffer.ToString(), Is.EqualTo(DataString()));

            buffer.Detach(socket);
        }

        [Test]
        public void Writes_data_received_synchronously_before_attached_to_socket()
        {
            Assert.That(drained, Is.False);
            WriteDataSync();
            Assert.That(drained, Is.False);

            buffer.Attach(socket);
            Assert.That(socket.Buffer.ToString(), Is.EqualTo(DataString()));

            buffer.End();

            Assert.That(drained, Is.True);
            Assert.That(socket.Buffer.ToString(), Is.EqualTo(DataString()));

            buffer.Detach(socket);
        }

        [Test]
        public void Writes_data_received_synchronously_before_and_after_attached_to_socket()
        {
            Assert.That(drained, Is.False);
            WriteDataSync();
            Assert.That(drained, Is.False);

            buffer.Attach(socket);
            Assert.That(drained, Is.False);
            Assert.That(socket.Buffer.ToString(), Is.EqualTo(DataString()));

            WriteDataSync();
            buffer.End();

            Assert.That(drained, Is.True);

            Assert.That(socket.Buffer.ToString(), Is.EqualTo(DataString() + DataString()));

            buffer.Detach(socket);
        }


        void WriteDataSync()
        {
            foreach (var d in NetTests.MakeData())
            {
                Assert.That(buffer.Write(new ArraySegment<byte>(d), null), Is.False, "Write did not return false");
            }
        }

        string DataString()
        {
            return NetTests.MakeData().Aggregate("", (s, d) => s + Encoding.UTF8.GetString(d));
        }

    }

    class MockSocket : ISocket
    {
        public DataBuffer Buffer;

        public MockSocket()
        {
            Buffer = new DataBuffer();
        }

        public bool Write(ArraySegment<byte> data, Action continuation)
        {
            // XXX do copy? 
            Buffer.AddToBuffer(data);
            return false;
        }

        public event EventHandler OnConnected;
        public event EventHandler<DataEventArgs> OnData;
        public event EventHandler OnEnd;
        public event EventHandler<ExceptionEventArgs> OnError;
        public event EventHandler OnClose;

        public System.Net.IPEndPoint RemoteEndPoint
        {
            get { throw new NotImplementedException(); }
        }

        public void Connect(System.Net.IPEndPoint ep)
        {
            throw new NotImplementedException();
        }


        public void End()
        {
            throw new NotImplementedException();
        }

        public void Dispose()
        {
            throw new NotImplementedException();
        }
    }
}
