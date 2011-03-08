//using System;
//using System.Collections.Generic;
//using Kayak.Http;
//using NUnit.Framework;

//namespace KayakTests
//{
//    [TestFixture]
//    public class TransactionTests
//    {
//        Transaction tx;

//        MockHttpServerDelegate serverDelegate;
//        Func<Version, bool, IResponse> makeResponse;
//        bool sync;

//        [SetUp]
//        public void SetUp()
//        {
//            //Debug.Listeners.Add(new TextWriterTraceListener(Console.Out));
//            //Debug.AutoFlush = true;
//            serverDelegate = new MockHttpServerDelegate();
//            makeResponse = (v, k) => new MockResponse();
//            tx = new Transaction(serverDelegate, makeResponse);
//            sync = false;
//        }

//        [TearDown]
//        public void TearDown()
//        {
//            //Debug.Listeners.Clear();
//        }

//        [Test]
//        public void HeadersEnd()
//        {
//            sync = tx.Execute(new ParserEvent()
//            {
//                Type = ParserEventType.RequestHeaders,
//                Request = new Request(),
//                KeepAlive = false
//            }, null);

//            Assert.IsTrue(sync);
//            Assert.AreEqual(1, serverDelegate.StartCalled);

//            sync = tx.Execute(new ParserEvent() { Type = ParserEventType.RequestEnded }, null);

//            Assert.IsTrue(sync);
//            Assert.AreEqual(1, serverDelegate.RequestDelegate.EndCalled);
//        }

//        [Test]
//        public void HeadersBodyEnd()
//        {
//            sync = tx.Execute(new ParserEvent()
//            {
//                Type = ParserEventType.RequestHeaders,
//                Request = new Request(),
//                KeepAlive = false
//            }, null);

//            Assert.IsTrue(sync);
//            Assert.AreEqual(1, serverDelegate.StartCalled);

//            Action c = () =>
//                {
//                    sync = tx.Execute(new ParserEvent() { Type = ParserEventType.RequestEnded }, null);

//                    Assert.IsTrue(sync);
//                    Assert.AreEqual(1, serverDelegate.RequestDelegate.EndCalled);
//                };

//            sync = tx.Execute(new ParserEvent() { Type = ParserEventType.RequestBody }, c);

//            Assert.IsFalse(sync);
//            Assert.AreEqual(1, serverDelegate.RequestDelegate.BodyCalled);
//            Assert.IsNotNull(serverDelegate.RequestDelegate.BodyContinuation);

//            serverDelegate.RequestDelegate.BodyContinuation();

//            Assert.IsTrue(sync);
//        }

//        [Test]
//        public void HeadersEndHeadersEnd()
//        {
//            sync = tx.Execute(new ParserEvent()
//            {
//                Type = ParserEventType.RequestHeaders,
//                Request = new Request(),
//                KeepAlive = true
//            }, null);

//            Assert.IsTrue(sync);
//            Assert.AreEqual(1, serverDelegate.StartCalled);

//            sync = tx.Execute(new ParserEvent() { Type = ParserEventType.RequestEnded }, null);

//            Assert.IsTrue(sync);
//            Assert.AreEqual(1, serverDelegate.RequestDelegate.EndCalled);

//            sync = tx.Execute(new ParserEvent()
//            {
//                Type = ParserEventType.RequestHeaders,
//                Request = new Request(),
//                KeepAlive = false
//            }, null);

//            Assert.IsTrue(sync);
//            Assert.AreEqual(2, serverDelegate.StartCalled);

//            sync = tx.Execute(new ParserEvent() { Type = ParserEventType.RequestEnded }, null);

//            Assert.IsTrue(sync);
//            Assert.AreEqual(2, serverDelegate.RequestDelegate.EndCalled);
//        }

//        [Test]
//        public void HeadersEndHeadersBodyEnd()
//        {
//            sync = tx.Execute(new ParserEvent()
//            {
//                Type = ParserEventType.RequestHeaders,
//                Request = new Request(),
//                KeepAlive = true
//            }, null);

//            Assert.IsTrue(sync);
//            Assert.AreEqual(1, serverDelegate.StartCalled);

//            sync = tx.Execute(new ParserEvent() { Type = ParserEventType.RequestEnded }, null);

//            Assert.IsTrue(sync);
//            Assert.AreEqual(1, serverDelegate.RequestDelegate.EndCalled);

//            sync = tx.Execute(new ParserEvent()
//            {
//                Type = ParserEventType.RequestHeaders,
//                Request = new Request(),
//                KeepAlive = false
//            }, null);

//            Assert.IsTrue(sync);
//            Assert.AreEqual(2, serverDelegate.StartCalled);

//            Action c = () =>
//            {
//                sync = tx.Execute(new ParserEvent() { Type = ParserEventType.RequestEnded }, null);

//                Assert.IsTrue(sync);
//                Assert.AreEqual(2, serverDelegate.RequestDelegate.EndCalled);
//            };

//            sync = tx.Execute(new ParserEvent() { Type = ParserEventType.RequestBody }, c);

//            Assert.IsFalse(sync);
//            Assert.AreEqual(1, serverDelegate.RequestDelegate.BodyCalled);
//            Assert.IsNotNull(serverDelegate.RequestDelegate.BodyContinuation);

//            serverDelegate.RequestDelegate.BodyContinuation();

//            Assert.IsTrue(sync);
//        }

//        //[Test]
//        //public void OneOhGetGet()
//        //{
//        //    var request1 = TestRequest.OneOhGetKeepAlive;
//        //    var request2 = TestRequest.OneOhGet;

//        //    eventSource = MockRequestEventSource.FromRequests(request1, request2);

//        //    var numRequests = 0;
//        //    var del = new MockRequestDelegate()
//        //    {
//        //        Start = (req, res, c, f) =>
//        //        {
//        //            if (numRequests == 0)
//        //                TestRequest.AssertAreEqual(request1, req);
//        //            else if (numRequests == 1)
//        //                TestRequest.AssertAreEqual(request2, req);
//        //            else
//        //                throw new Exception("Unexpected request count!");

//        //            numRequests++;

//        //            res.WriteHeaders("200 OK", null);
//        //            res.End(c, f);
//        //        },
//        //        End = (c, f) =>
//        //        {
//        //            c();
//        //        }
//        //    };

//        //    var numResponses = 0;
            
//        //    Func<Version, bool, IResponse> makeResponse = (v, k) => 
//        //    {
//        //        numResponses++;
//        //        return new MockResponse()
//        //        {
//        //            OnEnd = (c, f) => c()
//        //        };
//        //    };


//        //    var completed = false;
//        //    Transaction.Run(eventSource, makeResponse, del, () =>
//        //    {
//        //        completed = true;
//        //        Assert.AreEqual(2, del.StartCalled);
//        //        Assert.AreEqual(2, del.EndCalled);
//        //    },
//        //        e => { throw new Exception("Error during transaction.", e); });

//        //    Assert.IsTrue(completed);
//        //    Assert.AreEqual(numRequests, numResponses);
//        //}

//        //[Test]
//        //public void OneOhGetPost()
//        //{
//        //    var request1 = TestRequest.OneOhGetKeepAlive;
//        //    var request2 = TestRequest.OneOhPost;

//        //    eventSource = MockRequestEventSource.FromRequests(request1, request2);

//        //    var numRequests = 0;
//        //    var del = new MockRequestDelegate()
//        //    {
//        //        Start = (req, res, c, f) =>
//        //        {
//        //            if (numRequests == 0)
//        //                TestRequest.AssertAreEqual(request1, req);
//        //            else if (numRequests == 1)
//        //                TestRequest.AssertAreEqual(request2, req);
//        //            else
//        //                throw new Exception("Unexpected request count!");

//        //            numRequests++;

//        //            res.WriteHeaders("200 OK", null);
//        //            res.End(c, f);
//        //        },
//        //        Body = (data, c, f) =>
//        //        {
//        //            Assert.AreEqual(Encoding.UTF8.GetString(data.Array, data.Offset, data.Count), "hello world");
//        //            c();
//        //        },
//        //        End = (c, f) =>
//        //        {
//        //            c();
//        //        }
//        //    };

//        //    var numResponses = 0;
            
//        //    Func<Version, bool, IResponse> makeResponse = (v, k) => 
//        //    {
//        //        numResponses++;
//        //        return new MockResponse()
//        //        {
//        //            OnEnd = (c, f) => c()
//        //        };
//        //    };


//        //    var completed = false;
//        //    Transaction.Run(eventSource, makeResponse, del, () =>
//        //    {
//        //        completed = true;
//        //        Assert.AreEqual(2, del.StartCalled);
//        //        Assert.AreEqual(2, del.EndCalled);
//        //        Assert.AreEqual(1, del.BodyCalled);
//        //    },
//        //        e => { throw new Exception("Error during transaction.", e); });

//        //    Assert.IsTrue(completed);
//        //    Assert.AreEqual(numRequests, numResponses);
//        //}
//    }
//}
