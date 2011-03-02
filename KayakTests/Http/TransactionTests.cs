using System;
using System.Collections.Generic;
using Kayak.Http;
using NUnit.Framework;

namespace KayakTests
{
    [TestFixture]
    public class TransactionTests2
    {
        IEnumerable<HttpRequestEvent> events;
        MockRequestDelegate requestDelegate;
        Func<Version, bool, IResponse> makeResponse;
        bool sync;

        [SetUp]
        public void SetUp()
        {
            //Debug.Listeners.Add(new TextWriterTraceListener(Console.Out));
            //Debug.AutoFlush = true;
            requestDelegate = new MockRequestDelegate();
            makeResponse = (v, k) => new MockResponse();
            sync = false;
        }

        [TearDown]
        public void TearDown()
        {
            //Debug.Listeners.Clear();
        }

        [Test]
        public void HeadersEnd()
        {
            var tx = new Transaction2(requestDelegate, makeResponse);

            sync = tx.Execute(new HttpRequestEvent()
            {
                Type = HttpRequestEventType.RequestHeaders,
                Request = new Request(),
                KeepAlive = false
            }, null);

            Assert.IsTrue(sync);
            Assert.AreEqual(1, requestDelegate.StartCalled);

            sync = tx.Execute(new HttpRequestEvent() { Type = HttpRequestEventType.RequestEnded }, null);

            Assert.IsTrue(sync);
            Assert.AreEqual(1, requestDelegate.EndCalled);
        }

        [Test]
        public void HeadersBodyEnd()
        {
            var tx = new Transaction2(requestDelegate, makeResponse);

            sync = tx.Execute(new HttpRequestEvent()
            {
                Type = HttpRequestEventType.RequestHeaders,
                Request = new Request(),
                KeepAlive = false
            }, null);

            Assert.IsTrue(sync);
            Assert.AreEqual(1, requestDelegate.StartCalled);

            Action c = () =>
                {
                    sync = tx.Execute(new HttpRequestEvent() { Type = HttpRequestEventType.RequestEnded }, null);

                    Assert.IsTrue(sync);
                    Assert.AreEqual(1, requestDelegate.EndCalled);
                };

            sync = tx.Execute(new HttpRequestEvent() { Type = HttpRequestEventType.RequestBody }, c);

            Assert.IsFalse(sync);
            Assert.AreEqual(1, requestDelegate.BodyCalled);
            Assert.IsNotNull(requestDelegate.BodyContinuation);

            requestDelegate.BodyContinuation();

            Assert.IsTrue(sync);
        }

        [Test]
        public void HeadersEndHeadersEnd()
        {
            var tx = new Transaction2(requestDelegate, makeResponse);

            sync = tx.Execute(new HttpRequestEvent()
            {
                Type = HttpRequestEventType.RequestHeaders,
                Request = new Request(),
                KeepAlive = true
            }, null);

            Assert.IsTrue(sync);
            Assert.AreEqual(1, requestDelegate.StartCalled);

            sync = tx.Execute(new HttpRequestEvent() { Type = HttpRequestEventType.RequestEnded }, null);

            Assert.IsTrue(sync);
            Assert.AreEqual(1, requestDelegate.EndCalled);

            sync = tx.Execute(new HttpRequestEvent()
            {
                Type = HttpRequestEventType.RequestHeaders,
                Request = new Request(),
                KeepAlive = false
            }, null);

            Assert.IsTrue(sync);
            Assert.AreEqual(2, requestDelegate.StartCalled);

            sync = tx.Execute(new HttpRequestEvent() { Type = HttpRequestEventType.RequestEnded }, null);

            Assert.IsTrue(sync);
            Assert.AreEqual(2, requestDelegate.EndCalled);
        }

        [Test]
        public void HeadersEndHeadersBodyEnd()
        {
            var tx = new Transaction2(requestDelegate, makeResponse);

            sync = tx.Execute(new HttpRequestEvent()
            {
                Type = HttpRequestEventType.RequestHeaders,
                Request = new Request(),
                KeepAlive = true
            }, null);

            Assert.IsTrue(sync);
            Assert.AreEqual(1, requestDelegate.StartCalled);

            sync = tx.Execute(new HttpRequestEvent() { Type = HttpRequestEventType.RequestEnded }, null);

            Assert.IsTrue(sync);
            Assert.AreEqual(1, requestDelegate.EndCalled);

            sync = tx.Execute(new HttpRequestEvent()
            {
                Type = HttpRequestEventType.RequestHeaders,
                Request = new Request(),
                KeepAlive = false
            }, null);

            Assert.IsTrue(sync);
            Assert.AreEqual(2, requestDelegate.StartCalled);

            Action c = () =>
            {
                sync = tx.Execute(new HttpRequestEvent() { Type = HttpRequestEventType.RequestEnded }, null);

                Assert.IsTrue(sync);
                Assert.AreEqual(2, requestDelegate.EndCalled);
            };

            sync = tx.Execute(new HttpRequestEvent() { Type = HttpRequestEventType.RequestBody }, c);

            Assert.IsFalse(sync);
            Assert.AreEqual(1, requestDelegate.BodyCalled);
            Assert.IsNotNull(requestDelegate.BodyContinuation);

            requestDelegate.BodyContinuation();

            Assert.IsTrue(sync);
        }

        //[Test]
        //public void OneOhGetGet()
        //{
        //    var request1 = TestRequest.OneOhGetKeepAlive;
        //    var request2 = TestRequest.OneOhGet;

        //    eventSource = MockRequestEventSource.FromRequests(request1, request2);

        //    var numRequests = 0;
        //    var del = new MockRequestDelegate()
        //    {
        //        Start = (req, res, c, f) =>
        //        {
        //            if (numRequests == 0)
        //                TestRequest.AssertAreEqual(request1, req);
        //            else if (numRequests == 1)
        //                TestRequest.AssertAreEqual(request2, req);
        //            else
        //                throw new Exception("Unexpected request count!");

        //            numRequests++;

        //            res.WriteHeaders("200 OK", null);
        //            res.End(c, f);
        //        },
        //        End = (c, f) =>
        //        {
        //            c();
        //        }
        //    };

        //    var numResponses = 0;
            
        //    Func<Version, bool, IResponse> makeResponse = (v, k) => 
        //    {
        //        numResponses++;
        //        return new MockResponse()
        //        {
        //            OnEnd = (c, f) => c()
        //        };
        //    };


        //    var completed = false;
        //    Transaction.Run(eventSource, makeResponse, del, () =>
        //    {
        //        completed = true;
        //        Assert.AreEqual(2, del.StartCalled);
        //        Assert.AreEqual(2, del.EndCalled);
        //    },
        //        e => { throw new Exception("Error during transaction.", e); });

        //    Assert.IsTrue(completed);
        //    Assert.AreEqual(numRequests, numResponses);
        //}

        //[Test]
        //public void OneOhGetPost()
        //{
        //    var request1 = TestRequest.OneOhGetKeepAlive;
        //    var request2 = TestRequest.OneOhPost;

        //    eventSource = MockRequestEventSource.FromRequests(request1, request2);

        //    var numRequests = 0;
        //    var del = new MockRequestDelegate()
        //    {
        //        Start = (req, res, c, f) =>
        //        {
        //            if (numRequests == 0)
        //                TestRequest.AssertAreEqual(request1, req);
        //            else if (numRequests == 1)
        //                TestRequest.AssertAreEqual(request2, req);
        //            else
        //                throw new Exception("Unexpected request count!");

        //            numRequests++;

        //            res.WriteHeaders("200 OK", null);
        //            res.End(c, f);
        //        },
        //        Body = (data, c, f) =>
        //        {
        //            Assert.AreEqual(Encoding.UTF8.GetString(data.Array, data.Offset, data.Count), "hello world");
        //            c();
        //        },
        //        End = (c, f) =>
        //        {
        //            c();
        //        }
        //    };

        //    var numResponses = 0;
            
        //    Func<Version, bool, IResponse> makeResponse = (v, k) => 
        //    {
        //        numResponses++;
        //        return new MockResponse()
        //        {
        //            OnEnd = (c, f) => c()
        //        };
        //    };


        //    var completed = false;
        //    Transaction.Run(eventSource, makeResponse, del, () =>
        //    {
        //        completed = true;
        //        Assert.AreEqual(2, del.StartCalled);
        //        Assert.AreEqual(2, del.EndCalled);
        //        Assert.AreEqual(1, del.BodyCalled);
        //    },
        //        e => { throw new Exception("Error during transaction.", e); });

        //    Assert.IsTrue(completed);
        //    Assert.AreEqual(numRequests, numResponses);
        //}
    }
}
