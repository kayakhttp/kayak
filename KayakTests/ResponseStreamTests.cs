using System;
using System.Collections.Generic;
using System.Text;
using NUnit.Core;
using NUnit.Framework;
using System.IO;
using Kayak;

namespace KayakTests
{
    [TestFixture]
    public class ResponseStreamTests
    {
        ResponseStream responseStream;

        MemoryStream rest;
        string firstString, restString;

        int copyBufferSize;
        byte[] copyBuffer;
        MemoryStream destination;

        public void SetUp()
        {
            copyBuffer = new byte[copyBufferSize];

            firstString = "1234567";
            restString = "Some body or some shist. Blar Blah Blag Bleh.";
            rest = new MemoryStream(Encoding.UTF8.GetBytes(restString));

            destination = new SynchronousMemoryStream();
            responseStream = new ResponseStream(destination, Encoding.UTF8.GetBytes(firstString), restString.Length);
        }

        public void SyncWrite()
        {
            SetUp();
            while (true)
            {
                //Console.WriteLine("rest.length = " + rest.Length + " rest.position = " + rest.Position);
                var bytesRead = rest.Read(copyBuffer, 0, copyBuffer.Length);
                //Console.WriteLine("Read " + bytesRead + " bytes from source stream");
                if (bytesRead == 0)
                    break;

                responseStream.Write(copyBuffer, 0, bytesRead);
            }

            AssertWrite();
        }

        public void AssertWrite()
        {
            destination.Position = 0;
            string writtenString = null;

            Assert.AreEqual(firstString.Length + restString.Length, destination.Length, "Lengths differ.");

            using (var r = new StreamReader(destination, Encoding.UTF8))
                writtenString = r.ReadToEnd();

            Assert.AreEqual(firstString + restString, writtenString, "Written string was wrong.");
        }

        public void AsyncWrite()
        {
            SetUp();
            BeginWrite();

            AssertWrite();
        }

        public void BeginWrite()
        {
            var bytesRead = rest.Read(copyBuffer, 0, copyBuffer.Length);

            if (bytesRead == 0)
                return;

            //Console.WriteLine("writing " + bytesRead + " bytes");
            responseStream.BeginWrite(copyBuffer, 0, bytesRead, WriteCallback, null);
        }

        void WriteCallback(IAsyncResult asyncResult)
        {
            responseStream.EndWrite(asyncResult);
            BeginWrite();
        }

        [Test]
        public void SyncWrite1()
        {
            copyBufferSize = 1;
            SyncWrite();
        }

        [Test]
        public void SyncWrite2()
        {
            copyBufferSize = 2;
            SyncWrite();
        }

        [Test]
        public void SyncWrite3()
        {
            copyBufferSize = 3;
            SyncWrite();
        }

        [Test]
        public void SyncWrite4()
        {
            copyBufferSize = 4;
            SyncWrite();
        }

        [Test]
        public void SyncWrite5()
        {
            copyBufferSize = 5;
            SyncWrite();
        }

        [Test]
        public void AsyncWrite1()
        {
            copyBufferSize = 1;
            AsyncWrite();
        }

        [Test]
        public void AsyncWrite2()
        {
            copyBufferSize = 2;
            AsyncWrite();
        }

        [Test]
        public void AsyncWrite3()
        {
            copyBufferSize = 3;
            AsyncWrite();
        }

        [Test]
        public void AsyncWrite4()
        {
            copyBufferSize = 4;
            AsyncWrite();
        }

        [Test]
        public void AsyncWrite5()
        {
            copyBufferSize = 4;
            AsyncWrite();
        }
    }
}
