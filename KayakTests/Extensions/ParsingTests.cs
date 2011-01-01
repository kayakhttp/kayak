using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using System.IO;
using Moq;
using Kayak;

namespace KayakTests.Extensions
{
    [TestFixture]
    public class ReadRequestLineTests
    {
        Mock<TextReader> mockReader;

        [SetUp]
        public void SetUp()
        {
            mockReader = new Mock<TextReader>();
        }

        [Test]
        [ExpectedException(
            ExpectedException = typeof(Exception),
            ExpectedMessage = "Could not parse request status.")]
        public void EmptyStatusLine()
        {
            var reader = new StringReader("");

            reader.ReadRequestLine();
        }

        [Test]
        [ExpectedException(
            ExpectedException = typeof(Exception),
            ExpectedMessage = "Expected 2 or 3 tokens in request line.")]
        public void MalformedStatusLine()
        {
            var reader = new StringReader("asdasdf");

            reader.ReadRequestLine();
        }

        [Test]
        [ExpectedException(
            ExpectedException = typeof(Exception),
            ExpectedMessage = "Expected 2 or 3 tokens in request line.")]
        public void MalformedStatusLine3()
        {
            var reader = new StringReader("asdasd  fds sd dsfasdf");

            reader.ReadRequestLine();
        }

        [Test]
        [ExpectedException(
            ExpectedException = typeof(Exception),
            ExpectedMessage = "Expected 2 or 3 tokens in request line.")]
        public void MalformedStatusLine4()
        {
            var reader = new StringReader("asd asd d fs  aaaa    ");

            reader.ReadRequestLine();
        }

        [Test]
        public void DefaultHttpVersion()
        {
            var reader = new StringReader("asdas df");

            var requestLine = reader.ReadRequestLine();

            Assert.AreEqual("HTTP/1.0", requestLine.HttpVersion);
        }

        [Test]
        public void GivenHttpVersion()
        {
            var reader = new StringReader("asdas df asdf");

            var requestLine = reader.ReadRequestLine();

            Assert.AreEqual("asdf", requestLine.HttpVersion);
        }
    }
}
