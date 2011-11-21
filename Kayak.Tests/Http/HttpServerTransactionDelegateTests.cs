using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Kayak.Http;
using NUnit.Framework;

namespace Kayak.Tests.Http
{
    class UserCode
    {
        List<RequestInfo> receivedRequests = new List<RequestInfo>();
        RequestInfo current;

        public void OnRequest(HttpRequestHead head)
        {
            current = new RequestInfo() { Head = head, Data = Enumerable.Empty<string>() };
        }

        public void OnData(string data)
        {
            current.Data = current.Data.Concat(new[] { data });
        }

        public void OnError(Exception error)
        {
            current.Exception = error;
        }

        public void OnEnd()
        {
            if (current.Exception != null) throw new Exception("got end after exception");

            receivedRequests.Add(current);
            current = null;
        }

        public void AssertRequests(List<RequestInfo> expectedRequests)
        {
            for (int i = 0; i < expectedRequests.Count; i++)
            {
                if (receivedRequests.Count == i)
                    Assert.Fail("received fewer requests than expected");

                var received = receivedRequests[i];
                var expected = expectedRequests[i];
                Assert.That(received.Head.ToString(), Is.EqualTo(expected.Head.ToString()));
                Assert.That(received.Exception, Is.EqualTo(expected.Exception));
                Assert.That(received.Data, Is.EqualTo(expected.Data));
            }
        }
    }

    class MockTransaction : IHttpServerTransaction
    {
        List<ResponseInfo> receivedResponses = new List<ResponseInfo>();
        ResponseInfo current;

        public bool GotEnd;
        public bool GotDispose;

        public System.Net.IPEndPoint RemoteEndPoint
        {
            get { return null; }
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
            current.Data = current.Data.Concat(new[] { Encoding.ASCII.GetString(data.Array, data.Offset, data.Count) });
            return false;
        }

        public void OnResponseEnd()
        {
            receivedResponses.Add(current);
        }

        public void OnEnd()
        {
            if (GotEnd) throw new Exception("Got end already");
            GotEnd = true;
        }

        public void Dispose()
        {
            if (GotDispose) throw new Exception("Got dispose already");
            GotDispose = true;
        }

        public void AssertResponses(List<ResponseInfo> expectedResponses)
        {
            for (int i = 0; i < expectedResponses.Count; i++)
            {
                if (receivedResponses.Count == i)
                    Assert.Fail("Received fewer responses than expected.");

                var received = receivedResponses[i];
                var expected = expectedResponses[i];

                Assert.That(received.Head.ToString(), Is.EqualTo(expected.Head.ToString()));
                Assert.That(received.Exception, Is.EqualTo(expected.Exception));
                Assert.That(received.Data, Is.EqualTo(expected.Data));
            }
        }
    }

    class UserCodeTransactionInterface
    {
        UserCode userCode;
        IDisposable disconnect;
        IDataProducer requestBody; // XXX obtain
        IDataConsumer consumer; // XXX obtain

        IHttpResponseDelegate responseDelegate;

        public UserCodeTransactionInterface(UserCode userCode)
        {
            this.userCode = userCode;
        }

        public void SetRequestBodyAndResponseDelegate(IDataProducer requestBody, IHttpResponseDelegate responseDelegate)
        {
            this.requestBody = requestBody;
            this.responseDelegate = responseDelegate;
            this.disconnect = null;
        }

        public void ConnectRequestBody()
        {
            var consumer = new MockDataConsumer()
            {
                OnDataAction = data => userCode.OnData(Encoding.ASCII.GetString(data.Array, data.Offset, data.Count)),
                OnEndAction = () => userCode.OnEnd()
            };

            if (disconnect != null) throw new Exception("got connect and disconnect was not null");
            disconnect = requestBody.Connect(consumer);
        }

        public void DisconnectRequestBody()
        {
            disconnect.Dispose();
        }

        public void Respond(ResponseInfo response)
        {
            responseDelegate.OnResponse(response.Head, response.Data == null ? null : new MockDataProducer(c =>
            {
                foreach (var d in response.Data)
                    c.OnData(new ArraySegment<byte>(Encoding.ASCII.GetBytes(d)), null);
                c.OnEnd();
                return new Disposable(() => { });
            }));
        }
    }

    class KayakCodeTransactionInterface
    {
        IHttpServerTransactionDelegate del; // XXX obtain
        IHttpServerTransaction tx;

        public KayakCodeTransactionInterface(IHttpServerTransaction transaction, IHttpServerTransactionDelegate del)
        {
            tx = transaction;
            this.del = del;
        }

        public void OnRequest(RequestInfo request)
        {
            // XXX determine based on request info
            var shouldKeepAlive = false;

            del.OnRequest(tx, request.Head, shouldKeepAlive);
        }

        public void OnRequestData(string data)
        {
            del.OnRequestData(tx, new ArraySegment<byte>(Encoding.ASCII.GetBytes(data)), null);
        }

        public void OnRequestEnd()
        {
            del.OnRequestEnd(tx);
        }

        public void OnError(Exception e)
        {
            del.OnError(tx, e);
        }

        public void OnEnd()
        {
            del.OnEnd(tx);
        }
    }

    class RequestDelegate : IHttpRequestDelegate
    {
        UserCode userCode;
        UserCodeTransactionInterface userTransactionInterface;

        public RequestDelegate(UserCode userCode, UserCodeTransactionInterface userTransactionInterface)
        {
            this.userCode = userCode;
            this.userTransactionInterface = userTransactionInterface;
        }

        public void OnRequest(HttpRequestHead head, IDataProducer body, IHttpResponseDelegate response)
        {
            userCode.OnRequest(head);
            userTransactionInterface.SetRequestBodyAndResponseDelegate(body, response);
        }
    }

    class Consumer : IDataConsumer
    {
        UserCode userCode;

        public Consumer(UserCode userCode)
        {
            this.userCode = userCode;
        }

        public void OnError(Exception e)
        {
            userCode.OnError(e);
        }

        public bool OnData(ArraySegment<byte> data, Action continuation)
        {
            userCode.OnData(Encoding.ASCII.GetString(data.Array, data.Offset, data.Count));
            return false;
        }

        public void OnEnd()
        {
            userCode.OnEnd();
        }
    }

    class RequestInfo
    {
        public HttpRequestHead Head;
        public IEnumerable<string> Data;
        public Exception Exception;
    }

    class ResponseInfo
    {
        public HttpResponseHead Head;
        public IEnumerable<string> Data;
        public Exception Exception;
    }

    [TestFixture]
    class HttpServerTransactionDelegateTests
    {
        IHttpServerTransaction transaction;
        KayakCodeTransactionInterface kayakTransactionInterface;
        UserCode userCode;
        UserCodeTransactionInterface userTransactionInterface;

        [Test]
        public void Single_request_no_body()
        {
        }

        [Test]
        public void Multiple_requests_no_body()
        {
        }

        [Test]
        public void Single_request_with_body()
        {
            userCode = new UserCode();
            var transaction = new MockTransaction();
            userTransactionInterface = new UserCodeTransactionInterface(userCode);
            var requestDelegate = new RequestDelegate(userCode, userTransactionInterface);
            var transactionDelegate = new HttpServerTransactionDelegate(requestDelegate);
            kayakTransactionInterface = new KayakCodeTransactionInterface(transaction, transactionDelegate);

            var request = new RequestInfo()
            {
                Head = new HttpRequestHead()
                {
                    Version = new Version(1, 0)
                },
                Data = new[] { "hello ", "world." }
            };

            kayakTransactionInterface.OnRequest(request);

            foreach (var chunk in request.Data)
                kayakTransactionInterface.OnRequestData(chunk);

            kayakTransactionInterface.OnRequestEnd();

            kayakTransactionInterface.OnEnd();

            userTransactionInterface.ConnectRequestBody();

            var response = new ResponseInfo()
            {
                Head = new HttpResponseHead()
            };

            userTransactionInterface.Respond(response);

            response.Head.Headers = new Dictionary<string, string>();
            response.Head.Headers["Connection"] = "close";

            userCode.AssertRequests(new List<RequestInfo>(new[] { request }));
            transaction.AssertResponses(new List<ResponseInfo>(new[] { response }));
        }

        [Test]
        public void Multiple_requests_with_body()
        {
        }
    }
}
