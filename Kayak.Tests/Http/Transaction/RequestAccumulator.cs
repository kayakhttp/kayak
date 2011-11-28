using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Kayak.Http;

namespace Kayak.Tests.Http
{
    class RequestAccumulator : User
    {
        User next;
        List<RequestInfo> receivedRequests = new List<RequestInfo>();
        RequestInfo current;

        public RequestAccumulator(User next)
        {
            this.next = next;
        }

        public void OnRequest(UserKayak kayak, HttpRequestHead head)
        {
            current = new RequestInfo() { Head = head, Data = Enumerable.Empty<string>() };
            next.OnRequest(kayak, head);
        }

        public void OnRequestBodyData(UserKayak kayak, string data)
        {
            current.Data = current.Data.Concat(new[] { data });
            next.OnRequestBodyData(kayak, data);
        }

        public void OnRequestBodyError(UserKayak kayak, Exception error)
        {
            current.Exception = error;
            next.OnRequestBodyError(kayak, error);
        }

        public void OnRequestBodyEnd(UserKayak kayak)
        {
            if (current.Exception != null) throw new Exception("got end after exception");

            receivedRequests.Add(current);
            current = null;
            next.OnRequestBodyEnd(kayak);
        }

        public void AssertRequests(IEnumerable<RequestInfo> expectedRequests)
        {
            int i = 0;

            foreach (var expected in expectedRequests)
            {
                if (receivedRequests.Count == i)
                    Assert.Fail("received fewer requests than expected");

                var received = receivedRequests[i];
                Assert.That(received.Head.ToString(), Is.EqualTo(expected.Head.ToString()));
                Assert.That(received.Exception, Is.EqualTo(expected.Exception));
                
                if (expected.Data == null)
                    Assert.That(received.Data, Is.Null.Or.Empty);
                else
                    Assert.That(received.Data.Aggregate("", (acc, x) => acc += x), Is.EqualTo(expected.Data.Aggregate("", (acc, x) => acc += x)));

                i++;
            }
        }

        public void ConnectResponseBody(UserKayak kayak)
        {
            next.ConnectResponseBody(kayak);
        }

        public void DisconnectResponseBody(UserKayak kayak)
        {
            next.DisconnectResponseBody(kayak);
        }
    }
}
