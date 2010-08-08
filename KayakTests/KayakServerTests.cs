using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using NUnit.Framework;
using Kayak;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.IO;
using Moq;

//namespace KayakTests
//{
//    [TestFixture]
//    public class KayakServerTests
//    {

//        [Test]
//        public void RequestEvent()
//        {
//            var mockSocket = new Mock<ISocket>();
//            //mockSocket.Setup(s => s.Dispose()).Verifiable();
//            var mockContextFactory = new Mock<IKayakContextFactory>();
//            var mockContext = new Mock<IKayakContext>();

//            var socketSubject = new Subject<ISocket>();
//            var server = new KayakServer(socketSubject, null, mockContextFactory.Object);

//            mockContextFactory.Setup<IKayakContext>(cf => cf.CreateContext(It.IsAny<ISocket>())).Returns(() => mockContext.Object).Verifiable();

//            IKayakContext context = null;
//            bool completed = false;
//            var rx = server.Subscribe(c => context = c, () => completed = true);


//            socketSubject.OnNext(mockSocket.Object);
//            socketSubject.OnCompleted();

//            rx.Dispose();

//            //mockSocket.VerifyAll();
//            mockContextFactory.VerifyAll();
//            mockContext.VerifyAll();

//            Assert.AreEqual(mockContext.Object, context, "Server did not yield context.");
//            Assert.IsTrue(completed, "Server did not complete.");
//        }

//        [Test]
//        public void Request()
//        {
//            var request = Observable.Take(Observable.FromEvent<KayakContextEventArgs>(server, "Request"), 1);

//            server.Dispatch.Start();

//            bool gotRequest = false;
//            string verb = null, requestUri = null, httpVersion = null;
//            NameValueDictionary headers;

//            var s = request.Subscribe(Observer.Create((IEvent<KayakContextEventArgs> e) =>
//                {
//                    Console.WriteLine("Got request.");
//                    gotRequest = true;
//                    var req = e.EventArgs.Context.Request;
//                    verb = req.Verb;
//                    requestUri = req.RequestUri;
//                    httpVersion = req.HttpVersion;
//                    headers = req.Headers;
//                }));

//            client = new TcpClient();

//            Console.WriteLine("Client connecting.");
//            try
//            {
//                client.Connect(TestConstants.TestEndPoint.Address, TestConstants.TestEndPoint.Port);
//            }
//            catch (Exception e)
//            {
//                Console.WriteLine("Exception while connecting: " + e.Message);
//            }
//            Console.WriteLine("Client connected.");

//            string testVerb = "GET", testPath = "/some/path", testHttpVersion = "HTTP/1.0", testUA = "KayakTests";

//            string requestString = string.Format("{0} {1} {2}\r\nUser-Agent:{3}", testVerb, testPath, testHttpVersion, testU
//            Console.WriteLine("Wrote request.");
//            Console.WriteLine("Closing connection.");    
//            var clientStream = client;
//            Console.WriteLine("Connection closed.");
//            clientStream.Write(requestBytes, 0, requestBytes.Length);
//            clientStream.Flush();
//            Console.WriteLine("Wrote request.");
//            Console.WriteLine("Closing connection.");
//            client.Close();
//            Console.WriteLine("Connection closed.");

//            Console.WriteLine("Asking dispatch to stop.");
//            server.Dispatch.Stop();

//            s.Dispose();

//            Assert.IsTrue(gotRequest, "Never got request.");
//            Assert.AreEqual(testVerb, verb, "Got unexpected verb.");
//            Assert.AreEqual(testPath, requestUri, "Got unexpected request URI.");
//            Assert.AreEqual(testHttpVersion, httpVersion, "Got unexpected HTTP version.");
//        }
//    }
//}
