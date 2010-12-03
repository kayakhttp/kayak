using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Owin;
using Moq;
using Kayak;
using Kayak.Http;

namespace KayakTests.Extensions
{
    public class ResponderTests
    {
        
        [TestFixture]
        public class RespondToTests
        {
            Mock<IHttpSupport> mockHttpSupport;
            Mock<IHttpResponder> mockResponder;
            Mock<ISocket> mockSocket;
            Mock<IObserver<Unit>> mockObserver;

            [SetUp]
            public void SetUp()
            {
                mockHttpSupport = new Mock<IHttpSupport>();
                mockResponder = new Mock<IHttpResponder>();
                mockSocket = new Mock<ISocket>();
                mockObserver = new Mock<IObserver<Unit>>();
            }

            [Test]
            public void Test1()
            {
                mockResponder.Object.RespondTo(mockSocket.Object, mockHttpSupport.Object, null, null);

            }
        }
    }
}
