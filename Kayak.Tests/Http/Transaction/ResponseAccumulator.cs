using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Kayak.Http;
using NUnit.Framework;

namespace Kayak.Tests.Http
{
    class ResponseAccumulator : IHttpServerTransaction
    {
        List<ResponseInfo> receivedResponses = new List<ResponseInfo>();
        ResponseInfo current;

        public bool GotEnd;
        public bool GotDispose;

        public System.Net.IPEndPoint RemoteEndPoint
        {
            get;
            set;
        }

        public void OnResponse(HttpResponseHead response)
        {
            current = new ResponseInfo()
            {
                Head = response
            };
        }

        public bool OnResponseData(ArraySegment<byte> data, Action continuation)
        {
            if (current.Data == null)
                current.Data = Enumerable.Empty<string>();

            current.Data = current.Data.Concat(new[] { Encoding.ASCII.GetString(data.Array, data.Offset, data.Count) });
            return false;
        }

        public void OnResponseEnd()
        {
            if (current == null)
                throw new Exception("Transaction got OnResponseEnd when there was no current response");
            receivedResponses.Add(current);
            current = null;
        }

        public void OnEnd()
        {
            if (current != null) throw new Exception("Got OnEnd before OnResponseEnd");
            if (GotEnd) throw new Exception("Got end already");
            GotEnd = true;
        }

        public void Dispose()
        {
            if (GotDispose) throw new Exception("Got dispose already");
            GotDispose = true;
        }

        public void AssertResponses(IEnumerable<ResponseInfo> expectedResponses)
        {
            var i = 0;
            foreach (var expected in expectedResponses)
            {
                if (receivedResponses.Count == i)
                    Assert.Fail("Received fewer responses than expected.");

                var received = receivedResponses[i];

                Assert.That(received.Head.ToString(), Is.EqualTo(expected.Head.ToString()));
                Assert.That(received.Exception, Is.EqualTo(expected.Exception));

                if (expected.Data == null)
                    Assert.That(received.Data, Is.Null);
                else
                    Assert.That(received.Data.Aggregate("", (acc, x) => acc += x), Is.EqualTo(expected.Data.Aggregate("", (acc, x) => acc += x)));

                i++;
            }
        }
    }
}