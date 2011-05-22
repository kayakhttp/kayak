using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Kayak.Http;
using Kayak;
using KayakTests.Net;

namespace Kayak.Tests.Http
{
    [TestFixture]
    public class ParserToTransactionTransformTests
    {
        ParserToTransactionTransform del;
        MockHttpServerTransactionDelegate httpDel;


        [SetUp]
        public void SetUp()
        {
            httpDel = new MockHttpServerTransactionDelegate();
            del = new ParserToTransactionTransform(httpDel);
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

            httpDel.OnDataAction = (d, c) => { Assert.That(c, Is.Null); return false; };

            Commit(false);

            Assert.That(requestDelegates.Count, Is.EqualTo(1));
            Assert.That(requestDelegates[0].Data.ToString(), Is.EqualTo("and so it begins."));
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

            httpDel.OnDataAction = (d, c) => { Assert.That(c, Is.Null); return false; };

            Commit(false);

            Assert.That(requestDelegates.Count, Is.EqualTo(2));
            Assert.That(requestDelegates[0].Data.ToString(), Is.EqualTo("another late, bleary night of procrastination"));
            Assert.That(requestDelegates[1].Data.ToString(), Is.Empty);
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

            del.OnRequestBegan(new HttpRequestHeaders() { }, false);
            WriteBody("under the guise of creativity, some lofty goal");
            del.OnRequestEnded();

            httpDel.OnDataAction = (d, c) => { Assert.That(c, Is.Null); return false; };

            Commit(false);

            Assert.That(requestDelegates.Count, Is.EqualTo(2));
            Assert.That(requestDelegates[1].Data.ToString(), Is.EqualTo("under the guise of creativity, some lofty goal"));
        }

        [Test]
        public void Trailing_data_event_has_continuation()
        {
            del.OnRequestBegan(new HttpRequestHeaders() { }, true);
            WriteBody("hello world hello");

            var requestDelegates = httpDel.Requests;
            Assert.That(requestDelegates, Is.Empty);

            httpDel.OnDataAction = (d, c) => { Assert.That(c, Is.Not.Null); return true; };

            Commit(true);

            Assert.That(httpDel.current, Is.Not.Null);
            Assert.That(httpDel.current.Data.ToString(), Is.EqualTo("hello world hello"));

            WriteBody("whatever gets you off, you bastard");

            Commit(true);

            Assert.That(httpDel.current.Data.ToString(), Is.EqualTo("hello world hellowhatever gets you off, you bastard"));
        }

        [Test]
        public void Two_packets_one_request_sync()
        {
            del.OnRequestBegan(new HttpRequestHeaders() { }, true);
            WriteBody("hello world hello");

            var requestDelegates = httpDel.Requests;
            Assert.That(requestDelegates, Is.Empty);

            httpDel.OnDataAction = (d, c) => { Assert.That(c, Is.Not.Null); return false; };

            Commit(false);

            Assert.That(httpDel.current, Is.Not.Null);
            Assert.That(httpDel.current.Data.ToString(), Is.EqualTo("hello world hello"));

            WriteBody("whatever gets you off, you bastard");
            del.OnRequestEnded();

            httpDel.OnDataAction = (d, c) => { Assert.That(c, Is.Null); return false; };

            Commit(false);

            Assert.That(requestDelegates[0].Data.ToString(), Is.EqualTo("hello world hellowhatever gets you off, you bastard"));
        }

        [Test]
        public void Two_packets_one_request_async()
        {
            del.OnRequestBegan(new HttpRequestHeaders() { }, true);
            WriteBody("i am so incapable of stopping");

            var requestDelegates = httpDel.Requests;
            Assert.That(requestDelegates, Is.Empty);

            httpDel.OnDataAction = (d, c) => { Assert.That(c, Is.Not.Null); return true; };

            Commit(true);

            Assert.That(httpDel.current, Is.Not.Null);
            Assert.That(httpDel.current.Data.ToString(), Is.EqualTo("i am so incapable of stopping"));

            WriteBody("tired of this mandate");

            Assert.That(httpDel.current.Data.ToString(), Is.EqualTo("i am so incapable of stopping"));

            Commit(true);

            Assert.That(httpDel.current.Data.ToString(), Is.EqualTo("i am so incapable of stoppingtired of this mandate"));

            del.OnRequestEnded();

            Assert.That(requestDelegates.Count, Is.EqualTo(0));

            Commit(false);

            Assert.That(requestDelegates.Count, Is.EqualTo(1));
        }
    }
}
