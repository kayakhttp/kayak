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
    public class AttachableStreamTests
    {
        AttachableStream buffer;
        MockSocket socket;
        bool drained;

        [SetUp]
        public void SetUp()
        {
            drained = false;
            buffer = new AttachableStream(() => drained = true);
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
        }

        [Test]
        public void Writes_data_received_synchronously_and_ended_after_attached_to_socket()
        {
            buffer.Attach(socket);

            WriteDataSync();
            Assert.That(socket.Buffer.ToString(), Is.EqualTo(DataString()));

            Assert.That(drained, Is.False);
            buffer.End();
            Assert.That(drained, Is.True);

            Assert.That(socket.Buffer.ToString(), Is.EqualTo(DataString()));
        }

        [Test]
        public void Writes_data_received_synchronously_before_attached_to_socket()
        {
            WriteDataSync();

            buffer.Attach(socket);
            Assert.That(socket.Buffer.ToString(), Is.EqualTo(DataString()));

            Assert.That(drained, Is.False);
            buffer.End();
            Assert.That(drained, Is.True);
            Assert.That(socket.Buffer.ToString(), Is.EqualTo(DataString()));
        }

        [Test]
        public void Writes_data_received_synchronously_before_and_after_attached_to_socket()
        {
            WriteDataSync();

            buffer.Attach(socket);
            Assert.That(socket.Buffer.ToString(), Is.EqualTo(DataString()));

            WriteDataSync();
            Assert.That(socket.Buffer.ToString(), Is.EqualTo(DataString() + DataString()));

            Assert.That(drained, Is.False);
            buffer.End();
            Assert.That(drained, Is.True);

            Assert.That(socket.Buffer.ToString(), Is.EqualTo(DataString() + DataString()));
        }

        [Test]
        public void Delays_continuation_of_async_write_until_attached()
        {
            bool continuationInvoked = false;
            buffer.Write(
                new ArraySegment<byte>(Encoding.UTF8.GetBytes("Hello, hello.")),
                () => continuationInvoked = true);

            Assert.That(continuationInvoked, Is.False);
            buffer.Attach(socket);
            Assert.That(continuationInvoked, Is.True);
            Assert.That(socket.Buffer.ToString(), Is.EqualTo("Hello, hello."));

            Assert.That(drained, Is.False);
            buffer.End();
            Assert.That(drained, Is.True);

            Assert.That(socket.Buffer.ToString(), Is.EqualTo("Hello, hello."));
        }

        [Test]
        public void Delays_continuation_of_async_write_until_attached_followed_by_sync_write()
        {
            bool continuationInvoked = false;
            buffer.Write(
                new ArraySegment<byte>(Encoding.UTF8.GetBytes("Hello, hello.")),
                () => continuationInvoked = true);

            Assert.That(continuationInvoked, Is.False);

            buffer.Attach(socket);
            Assert.That(continuationInvoked, Is.True);
            Assert.That(socket.Buffer.ToString(), Is.EqualTo("Hello, hello."));

            buffer.Write(
                new ArraySegment<byte>(Encoding.UTF8.GetBytes("Hello, hello.")),
                null);

            Assert.That(socket.Buffer.ToString(), Is.EqualTo("Hello, hello.Hello, hello."));

            Assert.That(drained, Is.False);
            buffer.End();
            Assert.That(drained, Is.True);
            Assert.That(socket.Buffer.ToString(), Is.EqualTo("Hello, hello.Hello, hello."));
        }

        [Test]
        public void Delays_continuation_of_async_write_until_attached_followed_by_async_write()
        {
            bool continuation0Invoked = false;
            buffer.Write(
                new ArraySegment<byte>(Encoding.UTF8.GetBytes("Hello, hello.")),
                () => continuation0Invoked = true);

            Assert.That(continuation0Invoked, Is.False);

            buffer.Attach(socket);
            Assert.That(continuation0Invoked, Is.True);
            Assert.That(socket.Buffer.ToString(), Is.EqualTo("Hello, hello."));

            bool continuation1Invoked = false;
            buffer.Write(
                new ArraySegment<byte>(Encoding.UTF8.GetBytes("Hello, hello.")),
                () => continuation1Invoked = true);

            Assert.That(socket.Continuation, Is.Not.Null);
            socket.Continuation();
            Assert.That(continuation1Invoked, Is.True);
            Assert.That(socket.Buffer.ToString(), Is.EqualTo("Hello, hello.Hello, hello."));

            Assert.That(drained, Is.False);
            buffer.End();
            Assert.That(drained, Is.True);

            Assert.That(socket.Buffer.ToString(), Is.EqualTo("Hello, hello.Hello, hello."));
        }

        [Test]
        public void Delays_continuation_of_async_write_until_attached_prefixed_by_sync_write()
        {
            buffer.Write(
                new ArraySegment<byte>(Encoding.UTF8.GetBytes("Hello, hello.")),
                null);


            bool continuation0Invoked = false;
            buffer.Write(
                new ArraySegment<byte>(Encoding.UTF8.GetBytes("Hello, hello.")),
                () => continuation0Invoked = true);

            Assert.That(continuation0Invoked, Is.False);

            buffer.Attach(socket);
            Assert.That(continuation0Invoked, Is.True);
            Assert.That(socket.Buffer.ToString(), Is.EqualTo("Hello, hello.Hello, hello."));

            Assert.That(drained, Is.False);
            buffer.End();
            Assert.That(drained, Is.True);
            Assert.That(socket.Buffer.ToString(), Is.EqualTo("Hello, hello.Hello, hello."));
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
        public Action Continuation;

        public MockSocket()
        {
            Buffer = new DataBuffer();
        }

        public bool Write(ArraySegment<byte> data, Action continuation)
        {
            // XXX do copy? 
            Buffer.AddToBuffer(data);

            if (continuation != null)
            {
                Continuation = continuation;
                return true;
            }

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
