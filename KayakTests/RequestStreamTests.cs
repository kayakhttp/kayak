using System.Collections.Generic;
using System.Text;
using NUnit.Core;
using NUnit.Framework;
using Kayak;
using System.IO;
using System;

namespace KayakTests
{
    [TestFixture]
    public class RequestStreamTests
    {
        RequestStream requestStream;

        //byte[] source;
        SynchronousMemoryStream first, rest;
        string firstString, restString;

        int copyBufferSize = 1;
        int firstBufferSize = 1;

        byte[] copyBuffer;
        MemoryStream destination;
        int expectedLength;

        public void SetUp()
        {
            copyBuffer = new byte[copyBufferSize];
            destination = new MemoryStream();

            firstString = "This is, say, some headers or whatever.";
            restString = "This is, say, some body or whatever.";
            expectedLength = restString.Length;

            var sourceStream = new SynchronousMemoryStream(Encoding.UTF8.GetBytes(firstString + restString));

            // so we read a big hunk that goes into the body.

            var buffer = new byte[firstString.Length + firstBufferSize];

            sourceStream.Read(buffer, 0, buffer.Length);

            var firstBuffer = new byte[firstBufferSize];
            Buffer.BlockCopy(buffer, firstString.Length, firstBuffer, 0, firstBuffer.Length);

            //requestStream = new RequestStream(sourceStream, firstBuffer, restString.Length);
        }

        public string GetReadString()
        {
            destination.Position = 0;
            return new StreamReader(destination, Encoding.UTF8).ReadToEnd();
        }

        public void AsyncRead()
        {
            // since the underlying stream is synchronous, the read will be complete immediately.
            SetUp();
            BeginRead();

            destination.Position = 0;
            var readString = new StreamReader(destination, Encoding.UTF8).ReadToEnd();

            Assert.AreEqual(restString, GetReadString(), "Strings differ.");
        }

        void BeginRead()
        {
            //Console.WriteLine("BeginRead");
            requestStream.BeginRead(copyBuffer, 0, copyBuffer.Length, ReadCallback, null);
        }

        void ReadCallback(IAsyncResult iasr)
        {
            var bytesRead = requestStream.EndRead(iasr);
            //Console.WriteLine("Read " + bytesRead + " bytes.");
            destination.Write(copyBuffer, 0, bytesRead);
            //Console.WriteLine("destination.length = " + destination.Length);
            //Console.WriteLine("expectedLength = " + expectedLength);
            //Console.WriteLine();

            if (destination.Length < expectedLength && bytesRead != 0)
                BeginRead();
        }

        public void SyncRead()
        {
            SetUp();

            int bytesRead = 0;
            do
            {
                bytesRead = requestStream.Read(copyBuffer, 0, copyBuffer.Length);
                //Console.WriteLine("Read " + bytesRead + " bytes.");
                destination.Write(copyBuffer, 0, bytesRead);
            }
            while (destination.Length < expectedLength && bytesRead != 0);

            Assert.AreEqual(restString, GetReadString(), "Strings differ.");
        }

        [Test]
        public void AsyncRead1()
        {
            copyBufferSize = 1;
            firstBufferSize = 1;
            AsyncRead();
        }

        [Test]
        public void AsyncRead2()
        {
            copyBufferSize = 2;
            firstBufferSize = 2;
            AsyncRead();
        }

        [Test]
        public void AsyncRead3()
        {
            copyBufferSize = 4;
            firstBufferSize = 10;
            AsyncRead();
        }

        [Test]
        public void AsyncRead4()
        {
            copyBufferSize = 20;
            firstBufferSize = 16;
            AsyncRead();
        }

        [Test]
        public void SyncRead1()
        {
            copyBufferSize = 1;
            firstBufferSize = 1;
            SyncRead();
        }

        [Test]
        public void SyncRead2()
        {
            copyBufferSize = 2;
            firstBufferSize = 2;
            SyncRead();
        }

        [Test]
        public void SyncRead3()
        {
            copyBufferSize = 4;
            firstBufferSize = 2;
            SyncRead();
        }

        [Test]
        public void SyncRead4()
        {
            copyBufferSize = 20;
            firstBufferSize = 16;
            SyncRead();
        }
    }
}
