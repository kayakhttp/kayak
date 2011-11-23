using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Kayak.Http;
using NUnit.Framework;

namespace Kayak.Tests.Http
{
    interface UserCode
    {
        void OnRequest(KayakCode kayak, HttpRequestHead head);
        void OnRequestBodyData(KayakCode kayak, string data);
        void OnRequestBodyError(KayakCode kayak, Exception error);
        void OnRequestBodyEnd(KayakCode kayak);
        void ConnectResponseBody(KayakCode kayak);
        void DisconnectResponseBody(KayakCode kayak);
    }

    interface KayakCode
    {
        void OnResponse(HttpResponseHead head);
        void OnResponseBodyData(string data);
        void OnResponseBodyError(Exception error);
        void OnResponseBodyEnd();
        void ConnectRequestBody();
        void DisconnectRequestBody();
    }

    class UserCodeCallbacks : UserCode
    {
        public Action OnRequestAction;
        public Action ConnectResponseBodyAction;

        public void OnRequest(KayakCode kayak, HttpRequestHead head)
        {
            if (OnRequestAction != null)
                OnRequestAction();
        }

        public void OnRequestBodyData(KayakCode kayak, string data)
        {
        }

        public void OnRequestBodyError(KayakCode kayak, Exception error)
        {
        }

        public void OnRequestBodyEnd(KayakCode kayak)
        {
        }

        public void ConnectResponseBody(KayakCode kayak)
        {
            if (ConnectResponseBodyAction != null)
                ConnectResponseBodyAction();
        }

        public void DisconnectResponseBody(KayakCode kayak)
        {
        }
    }


    class RequestAccumulator : UserCode
    {
        UserCode next;
        List<RequestInfo> receivedRequests = new List<RequestInfo>();
        RequestInfo current;

        public RequestAccumulator(UserCode next)
        {
            this.next = next;
        }

        public void OnRequest(KayakCode kayak, HttpRequestHead head)
        {
            current = new RequestInfo() { Head = head, Data = Enumerable.Empty<string>() };
            next.OnRequest(kayak, head);
        }

        public void OnRequestBodyData(KayakCode kayak, string data)
        {
            current.Data = current.Data.Concat(new[] { data });
            next.OnRequestBodyData(kayak, data);
        }

        public void OnRequestBodyError(KayakCode kayak, Exception error)
        {
            current.Exception = error;
            next.OnRequestBodyError(kayak, error);
        }

        public void OnRequestBodyEnd(KayakCode kayak)
        {
            if (current.Exception != null) throw new Exception("got end after exception");

            receivedRequests.Add(current);
            current = null;
            next.OnRequestBodyEnd(kayak);
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

        public void ConnectResponseBody(KayakCode kayak)
        {
            next.ConnectResponseBody(kayak);
        }

        public void DisconnectResponseBody(KayakCode kayak)
        {
            next.DisconnectResponseBody(kayak);
        }
    }

    class MockTransaction : IHttpServerTransaction
    {
        List<ResponseInfo> receivedResponses = new List<ResponseInfo>();
        ResponseInfo current;
        IEnumerable<string> currentData;

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
            if (currentData == null)
                currentData = Enumerable.Empty<string>();

            currentData = currentData.Concat(new[] { Encoding.ASCII.GetString(data.Array, data.Offset, data.Count) });
            return false;
        }

        public void OnResponseEnd()
        {
            var theData = currentData;
            current.Data = theData;
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

    class UserCodeTransactionInterface : KayakCode
    {
        UserCode userCode;
        IDisposable disconnect;
        IDataProducer requestBody;

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
                OnDataAction = data => userCode.OnRequestBodyData(this, Encoding.ASCII.GetString(data.Array, data.Offset, data.Count)),
                OnEndAction = () => userCode.OnRequestBodyEnd(this)
            };

            if (disconnect != null) throw new Exception("got connect and disconnect was not null");
            disconnect = requestBody.Connect(consumer);
        }

        public void DisconnectRequestBody()
        {
            disconnect.Dispose();
        }

        SimpleSubject subject;

        public void OnResponse(HttpResponseHead head)
        {
            subject = new SimpleSubject(
                () => userCode.ConnectResponseBody(this), 
                () => userCode.DisconnectResponseBody(this));

            responseDelegate.OnResponse(head, subject);
        }

        public void OnResponseBodyData(string data)
        {
            subject.OnData(new ArraySegment<byte>(Encoding.ASCII.GetBytes(data)), null);
        }

        public void OnResponseBodyError(Exception error)
        {
            subject.OnError(error);
        }

        public void OnResponseBodyEnd()
        {
            subject.OnEnd();
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

        public void OnClose()
        {
            del.OnClose(tx);
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
            userTransactionInterface.SetRequestBodyAndResponseDelegate(body, response);
            userCode.OnRequest(userTransactionInterface, head);
        }
    }

    class Consumer : IDataConsumer
    {
        UserCode userCode;
        KayakCode kayak;

        public Consumer(UserCode userCode, KayakCode kayak)
        {
            this.userCode = userCode;
            this.kayak = kayak;
        }

        public void OnError(Exception e)
        {
            userCode.OnRequestBodyError(kayak, e);
        }

        public bool OnData(ArraySegment<byte> data, Action continuation)
        {
            userCode.OnRequestBodyData(kayak, Encoding.ASCII.GetString(data.Array, data.Offset, data.Count));
            return false;
        }

        public void OnEnd()
        {
            userCode.OnRequestBodyEnd(kayak);
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
        UserCodeCallbacks userCodeCallbacks;
        MockTransaction mockTransaction;
        KayakCodeTransactionInterface kayakTransactionInterface;
        RequestAccumulator requestAccumulator;
        UserCodeTransactionInterface userTransactionInterface;

        [SetUp]
        public void SetUp()
        {
            userCodeCallbacks = new UserCodeCallbacks();
            requestAccumulator = new RequestAccumulator(userCodeCallbacks);
            var transaction = new MockTransaction();
            userTransactionInterface = new UserCodeTransactionInterface(requestAccumulator);
            var requestDelegate = new RequestDelegate(requestAccumulator, userTransactionInterface);
            var transactionDelegate = new HttpServerTransactionDelegate(requestDelegate);
            kayakTransactionInterface = new KayakCodeTransactionInterface(transaction, transactionDelegate);
        }


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
            var request = new RequestInfo()
            {
                Head = new HttpRequestHead()
                {
                    Version = new Version(1, 0)
                },
                Data = new[] { "hello ", "world." }
            };

            IEnumerable<string> responseData = new[] { "yo", "dawg." };
            var response = new ResponseInfo()
            {
                Head = new HttpResponseHead(),
                Data = responseData
            };

            userCodeCallbacks.OnRequestAction = () =>
            {
                userTransactionInterface.OnResponse(response.Head);
                userTransactionInterface.ConnectRequestBody();
            };

            userCodeCallbacks.ConnectResponseBodyAction = () =>
            {
                foreach (var data in responseData)
                    userTransactionInterface.OnResponseBodyData(data);

                userTransactionInterface.OnResponseBodyEnd();
            };

            kayakTransactionInterface.OnRequest(request);

            foreach (var chunk in request.Data)
                kayakTransactionInterface.OnRequestData(chunk);

            kayakTransactionInterface.OnRequestEnd();

            kayakTransactionInterface.OnEnd();
            kayakTransactionInterface.OnClose();

            response.Head.Headers = new Dictionary<string, string>();
            response.Head.Headers["Connection"] = "close";

            requestAccumulator.AssertRequests(new List<RequestInfo>(new[] { request }));
            mockTransaction.AssertResponses(new List<ResponseInfo>(new[] { response }));
            Assert.That(mockTransaction.GotEnd, Is.True);
            Assert.That(mockTransaction.GotDispose, Is.True);
        }

        [Test]
        public void Multiple_requests_with_body()
        {
        }
    }

    public class SimpleSubject : IDataConsumer, IDataProducer
    {
        IDataConsumer consumer;
        Action disconnect;
        Action connect;

        public SimpleSubject(Action connect, Action disconnect)
        {
            this.connect = connect;
            this.disconnect = disconnect;
        }

        public void OnError(Exception e)
        {
            consumer.OnError(e);
        }

        public bool OnData(ArraySegment<byte> data, Action continuation)
        {
            return consumer.OnData(data, continuation);
        }

        public void OnEnd()
        {
            consumer.OnEnd();
        }

        public IDisposable Connect(IDataConsumer channel)
        {
            this.consumer = channel;
            connect();
            return new Disposable(disconnect);
        }
    }
}
