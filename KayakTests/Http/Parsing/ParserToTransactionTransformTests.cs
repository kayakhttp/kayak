using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Kayak.Http;
using Kayak;
using KayakTests.Net;

namespace KayakTests.Http
{
    //[TestFixture]
    //public class EventQueueTests
    //{
    //    bool error;

    //    // same instance
    //    IHttpEventQueue queue;
    //    IParserDelegate parserDelegate;

    //    [Test]
    //    public void Transforms_to_events()
    //    {
    //        parserDelegate.OnRequestBegan(new HttpRequestHeaders(), false);
    //        parserDelegate.OnRequestBody(new ArraySegment<byte>());
    //        parserDelegate.OnRequestEnded();

    //        var e = queue.Dequeue();
    //        Assert.That(e.Type, Is.EqualTo(ParserEventType.RequestHeaders));
    //        Assert.That(e.KeepAlive, Is.False);

    //        e = queue.Dequeue();
    //        Assert.That(e.Type, Is.EqualTo(ParserEventType.RequestBody));

    //        e = queue.Dequeue();
    //        Assert.That(e.Type, Is.EqualTo(ParserEventType.RequestEnded));

    //        // etc. lots of permutations on when OnWhatever is called, when Dequeue is called,
    //        // when Delay is called (and verify rebuffering), error.
    //    }

    //    // but that's not interesting really.

    //}

    //[TestFixture]
    //public class EventConsumerTests
    //{
    //    bool willInvokeContinuation;
    //    MockRequestDelegate requestDelegate;
    //    IHttpEventHandler handler;



    //    [Test]
    //    public void Transforms_to_continuations()
    //    {
    //        willInvokeContinuation = handler.ProcessEvent(
    //            new ParserEvent() { Type = ParserEventType.RequestHeaders }, requestDelegate, () => { });

    //        Assert.That(willInvokeContinuation, Is.False);
    //        // assert mock got OnRequestHeaders

    //        willInvokeContinuation = handler.ProcessEvent(
    //            new ParserEvent() { Type = ParserEventType.RequestBody }, requestDelegate, () => { });

    //        Assert.That(willInvokeContinuation, Is.False);
    //        // assert mock got OnRequestBody. vary on willInvokeContinuation

    //        willInvokeContinuation = handler.ProcessEvent(
    //            new ParserEvent() { Type = ParserEventType.RequestEnded }, requestDelegate, () => { });

    //        Assert.That(willInvokeContinuation, Is.False);
    //        // assert mock got OnRequestEnded. 
    //        // assert error if ProcessEvent called before continuation invoked if previous call returned true. etc.
    //    }
    //}

    class MockHttpDel : IHttpServerTransactionDelegate, IDisposable
    {
        public List<MockIRequestDelegate> Requests;
        public bool GotOnEnd;
        public Func<Func<ArraySegment<byte>, Action, bool>> OnDataFactory;

        public MockHttpDel()
        {
            Requests = new List<MockIRequestDelegate>();
        }

        public void OnBegin(ISocket socket)
        {
        }

        public void OnRequest(IHttpServerRequest request, bool shouldKeepAlive)
        {
            Requests.Add(new MockIRequestDelegate(request, shouldKeepAlive) { OnData = OnDataFactory == null ? null : OnDataFactory() });
        }

        public void OnEnd()
        {
            if (GotOnEnd)
                throw new Exception("OnEnd was called more than once.");

            GotOnEnd = true;
        }

        public void Dispose()
        {
            foreach (var di in Requests)
                di.Dispose();
        }

        public class MockIRequestDelegate : IDisposable
        {
            public IHttpServerRequest Request;
            public bool ShouldKeepAlive;
            public Action OnEnd;
            public Func<ArraySegment<byte>, Action, bool> OnData;
            public int NumOnEndEvents;
            public int NumOnBodyEvents;
            public DataBuffer Buffer;

            public MockIRequestDelegate(IHttpServerRequest request, bool shouldKeepAlive)
            {
                Request = request;
                ShouldKeepAlive = shouldKeepAlive;
                request.OnBody += request_OnBody;
                request.OnEnd += request_OnEnd;
                Buffer = new DataBuffer();

            }

            void request_OnBody(object sender, DataEventArgs e)
            {
                NumOnBodyEvents++;
                e.WillInvokeContinuation = false;

                Buffer.AddToBuffer(e.Data);

                if (OnData != null)
                    e.WillInvokeContinuation = OnData(e.Data, e.Continuation);
            }

            void request_OnEnd(object sender, EventArgs e)
            {
                NumOnEndEvents++;
                if (OnEnd != null)
                    OnEnd();
            }

            public void Dispose()
            {
                Request.OnBody -= request_OnBody;
                Request.OnEnd -= request_OnEnd;
            }
        }
    }



    [TestFixture]
    public class ParserToTransactionTransformTests
    {
        ParserToTransactionTransform del;
        MockHttpDel httpDel;
        

        [SetUp]
        public void SetUp()
        {
            httpDel = new MockHttpDel();
            del = new ParserToTransactionTransform(httpDel);
        }

        [TearDown]
        public void TearDown()
        {
            httpDel.Dispose();
        }

        void WriteBody(string data)
        {
            del.OnRequestBody(new ArraySegment<byte>(Encoding.UTF8.GetBytes(data)));
        }

        void Commit(bool shouldInvokeContinuation)
        {
            bool continuationInvoked = false;
            var result = del.Commit(() => { continuationInvoked = true; });

            Assert.That(result, Is.EqualTo(shouldInvokeContinuation));
            Assert.That(continuationInvoked, Is.False);
        }

        [Test]
        public void One_packet_one_request()
        {
            del.OnRequestBegan(new HttpRequestHeaders() { }, false);
            WriteBody("and so it begins.");
            del.OnRequestEnded();


            var requestDelegates = httpDel.Requests;
            Assert.That(requestDelegates, Is.Empty);

            httpDel.OnDataFactory = () => (d, c) => { Assert.That(c, Is.Null); return false; };

            Commit(false);

            Assert.That(requestDelegates.Count, Is.EqualTo(1));
            Assert.That(requestDelegates[0].NumOnBodyEvents, Is.EqualTo(1));
            Assert.That(requestDelegates[0].Buffer.ToString(), Is.EqualTo("and so it begins."));
            Assert.That(requestDelegates[0].NumOnEndEvents, Is.EqualTo(1));
        }

        [Test]
        public void One_packet_two_requests()
        {
            del.OnRequestBegan(new HttpRequestHeaders() { }, true);
            WriteBody("another late, bleary night of procrastination");
            del.OnRequestEnded();
            del.OnRequestBegan(new HttpRequestHeaders() { }, false);
            del.OnRequestEnded();

            var requestDelegates = httpDel.Requests;

            Assert.That(requestDelegates, Is.Empty);

            httpDel.OnDataFactory = () => (d, c) => { Assert.That(c, Is.Null); return false; };

            Commit(false);

            Assert.That(requestDelegates.Count, Is.EqualTo(2));
            Assert.That(requestDelegates[0].NumOnBodyEvents, Is.EqualTo(1));
            Assert.That(requestDelegates[0].Buffer.ToString(), Is.EqualTo("another late, bleary night of procrastination"));
            Assert.That(requestDelegates[0].NumOnEndEvents, Is.EqualTo(1));
            Assert.That(requestDelegates[1].NumOnBodyEvents, Is.EqualTo(0));
            Assert.That(requestDelegates[1].NumOnEndEvents, Is.EqualTo(1));
        }

        [Test]
        public void Two_packets_two_requests()
        {
            del.OnRequestBegan(new HttpRequestHeaders() { }, true);
            del.OnRequestEnded();

            var requestDelegates = httpDel.Requests;
            Assert.That(requestDelegates, Is.Empty);

            Commit(false);

            Assert.That(requestDelegates.Count, Is.EqualTo(1));
            Assert.That(requestDelegates[0].NumOnBodyEvents, Is.EqualTo(0));
            Assert.That(requestDelegates[0].NumOnEndEvents, Is.EqualTo(1));

            del.OnRequestBegan(new HttpRequestHeaders() { }, false);
            WriteBody("under the guise of creativity, some lofty goal");
            del.OnRequestEnded();

            httpDel.OnDataFactory = () => (d, c) => { Assert.That(c, Is.Null); return false; };

            Commit(false);

            Assert.That(requestDelegates.Count, Is.EqualTo(2));
            Assert.That(requestDelegates[0].NumOnBodyEvents, Is.EqualTo(0));
            Assert.That(requestDelegates[0].NumOnEndEvents, Is.EqualTo(1));
            Assert.That(requestDelegates[1].NumOnBodyEvents, Is.EqualTo(1));
            Assert.That(requestDelegates[1].Buffer.ToString(), Is.EqualTo("under the guise of creativity, some lofty goal"));
            Assert.That(requestDelegates[1].NumOnEndEvents, Is.EqualTo(1));
        }

        [Test]
        public void Trailing_data_event_has_continuation()
        {
            del.OnRequestBegan(new HttpRequestHeaders() { }, true);
            WriteBody("hello world hello");

            var requestDelegates = httpDel.Requests;
            Assert.That(requestDelegates, Is.Empty);

            httpDel.OnDataFactory = () => (d, c) => { Assert.That(c, Is.Not.Null); return true; };

            Commit(true);

            Assert.That(requestDelegates.Count, Is.EqualTo(1));
            Assert.That(requestDelegates[0].NumOnBodyEvents, Is.EqualTo(1));

            WriteBody("whatever gets you off, you bastard");

            Commit(true);

            Assert.That(requestDelegates[0].Buffer.ToString(), Is.EqualTo("hello world hellowhatever gets you off, you bastard"));
        }

        [Test]
        public void Two_packets_one_request_sync()
        {
            del.OnRequestBegan(new HttpRequestHeaders() { }, true);
            WriteBody("hello world hello");


            var requestDelegates = httpDel.Requests;
            Assert.That(requestDelegates, Is.Empty);

            httpDel.OnDataFactory = () => (d, c) => { Assert.That(c, Is.Not.Null); return false; };

            Commit(false);

            Assert.That(requestDelegates.Count, Is.EqualTo(1));
            Assert.That(requestDelegates[0].NumOnBodyEvents, Is.EqualTo(1));

            WriteBody("whatever gets you off, you bastard");
            del.OnRequestEnded();

            requestDelegates[0].OnData = (d, c) => { Assert.That(c, Is.Null); return false; };

            Commit(false);

            Assert.That(requestDelegates[0].Buffer.ToString(), Is.EqualTo("hello world hellowhatever gets you off, you bastard"));
            Assert.That(requestDelegates[0].NumOnEndEvents, Is.EqualTo(1));
        }

        [Test]
        public void Two_packets_one_request_async()
        {
            del.OnRequestBegan(new HttpRequestHeaders() { }, true);
            WriteBody("i am so incapable of stopping");

            var requestDelegates = httpDel.Requests;
            Assert.That(requestDelegates, Is.Empty);

            httpDel.OnDataFactory = () => (d, c) => { Assert.That(c, Is.Not.Null); return true; };

            Commit(true);

            Assert.That(requestDelegates.Count, Is.EqualTo(1));
            Assert.That(requestDelegates[0].NumOnBodyEvents, Is.EqualTo(1));

            WriteBody("tired of this mandate");

            requestDelegates[0].OnData = (d, c) => { Assert.That(c, Is.Not.Null); return true; };

            Assert.That(requestDelegates[0].NumOnBodyEvents, Is.EqualTo(1));

            Commit(true);

            Assert.That(requestDelegates[0].NumOnBodyEvents, Is.EqualTo(2));
            Assert.That(requestDelegates[0].Buffer.ToString(), Is.EqualTo("i am so incapable of stoppingtired of this mandate"));


            del.OnRequestEnded();

            Assert.That(requestDelegates[0].NumOnEndEvents, Is.EqualTo(0));

            Commit(false);

            Assert.That(requestDelegates[0].NumOnEndEvents, Is.EqualTo(1));
        }
    }
}
