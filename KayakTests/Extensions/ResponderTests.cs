using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Kayak.Core;
using Moq;
using Kayak;

namespace KayakTests.Extensions
{
    public class ResponderTests
    {
        
        [TestFixture]
        public class RespondToTests
        {
            Mock<IHttpResponder> mockResponder;
            Mock<ISocket> mockSocket;
            Mock<IObserver<Unit>> mockObserver;

            [SetUp]
            public void SetUp()
            {
                mockResponder = new Mock<IHttpResponder>();
                mockSocket = new Mock<ISocket>();
                mockObserver = new Mock<IObserver<Unit>>();
            }

            [Test]
            public void Test1()
            {
            }
        }
    }
}
