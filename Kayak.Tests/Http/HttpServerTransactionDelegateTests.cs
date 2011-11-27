using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Kayak.Http;
using NUnit.Framework;

namespace Kayak.Tests.Http
{
    // represents all the ways kayak can call user code
    interface User
    {
        void OnRequest(UserKayak kayak, HttpRequestHead head);
        void OnRequestBodyData(UserKayak kayak, string data);
        void OnRequestBodyError(UserKayak kayak, Exception error);
        void OnRequestBodyEnd(UserKayak kayak);
        void ConnectResponseBody(UserKayak kayak);
        void DisconnectResponseBody(UserKayak kayak);
    }

    // represents all the ways user code can call kayak
    interface UserKayak
    {
        void OnResponse(HttpResponseHead head);
        void OnResponseBodyData(string data);
        void OnResponseBodyError(Exception error);
        void OnResponseBodyEnd();
        void ConnectRequestBody();
        void DisconnectRequestBody();
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
        RequestCallbacker requestCallbacker;
        RequestAccumulator requestAccumulator;

        ResponseAccumulator responseAccumulator;
        TransactionInput transactionInput;

        Queue<Action> postedActions;

        [SetUp]
        public void SetUp()
        {
            requestCallbacker = new RequestCallbacker();
            requestAccumulator = new RequestAccumulator(requestCallbacker);
            var requestDelegate = new RequestDelegate(requestAccumulator);
            var transactionDelegate = new HttpServerTransactionDelegate(requestDelegate);
            responseAccumulator = new ResponseAccumulator();
            transactionInput = new TransactionInput(responseAccumulator, transactionDelegate);

            postedActions = new Queue<Action>();
        }

        public class InputOutput
        {
            public string Name;
            public IEnumerable<RequestInfo> Requests;
            public IEnumerable<ResponseInfo> UserResponses;
            public IEnumerable<ResponseInfo> ExpectedResponses;

            public override string ToString()
            {
                return Name;
            }

            class Request
            {
                public static RequestInfo OneOhKeepAliveWithBody = new RequestInfo()
                {
                    Head = new HttpRequestHead()
                    {
                        Version = new Version(1, 0),
                        Headers = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase) 
                    {
                        { "Connection", "keep-alive" }
                    }
                    },
                    Data = new[] { "hello ", "world." }
                };

                public static RequestInfo OneOhWithBody = new RequestInfo()
                {
                    Head = new HttpRequestHead()
                    {
                        Version = new Version(1, 0),
                        Headers = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase) 
                    {
                        { "X-Foo", "bar" }
                    }
                    },
                    Data = new[] { "hello ", "world!" }
                };
            }

            class Response
            {
                public static ResponseInfo TwoHundredOKWithBody = new ResponseInfo()
                {
                    Head = new HttpResponseHead()
                    {
                        Status = "200 OK"
                    },
                    Data = new[] { "yo ", "dawg." }
                };

                public static ResponseInfo TwoHundredOKConnectionCloseWithBody = new ResponseInfo()
                {
                    Head = new HttpResponseHead()
                    {
                        Status = "200 OK",
                        Headers = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase)
                        {
                            { "Connection", "close" }
                        }
                    },
                    Data = new[] { "yo ", "dawg." }
                };
            }

            public static IEnumerable<InputOutput> GetValues()
            {
                yield return new InputOutput()
                {
                    Name = "1.0 single request response",
                    Requests = new[] { Request.OneOhWithBody },
                    UserResponses = new[] { Response.TwoHundredOKWithBody },
                    ExpectedResponses = new[] { Response.TwoHundredOKWithBody }.Select(r =>
                    {
                        r.Head.Headers = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);
                        r.Head.Headers["Connection"] = "close";
                        return r;
                    })
                };

                yield return new InputOutput()
                {
                    Name = "1.0 two request response",
                    Requests = new[] { Request.OneOhKeepAliveWithBody, Request.OneOhWithBody },
                    UserResponses = new[] { Response.TwoHundredOKWithBody, Response.TwoHundredOKWithBody },
                    ExpectedResponses = new[] { Response.TwoHundredOKWithBody, Response.TwoHundredOKConnectionCloseWithBody }
                };
            }
        }

        IEnumerable<Action> GetInputActions(IEnumerable<RequestInfo> requests, TransactionInput txInput)
        {
            foreach (var request in requests)
            {
                yield return () => transactionInput.OnRequest(request);

                foreach (var chunk in request.Data)
                    yield return () => transactionInput.OnRequestData(chunk);

                yield return () => transactionInput.OnRequestEnd();

            }

            yield return () => transactionInput.OnEnd();
            yield return () => transactionInput.OnClose();
        }

        void Post(Action action, bool defer)
        {
            if (defer)
                postedActions.Enqueue(action);
            else action();
        }

        IEnumerable<Action> GetPostedActions()
        {
            while (true)
            {
                if (postedActions.Count == 0)
                    yield return null;
                else
                    yield return postedActions.Dequeue();
            }
        }

        IEnumerable<Action> Interleave(params IEnumerator<Action>[] sources)
        {
            var done = new List<IEnumerator<Action>>();

            while (true)
            {
                bool didSomething = false;

                foreach (var source in sources)
                {
                    if (source.MoveNext())
                    {
                        var a = source.Current;

                        if (a != null)
                        {
                            didSomething = true;
                            yield return a;
                        }
                        else
                            done.Add(source);
                    }
                    else
                        done.Add(source);
                }

                if (!didSomething)
                    yield break;
            }

        }

        void Drive(IEnumerable<Action> actions)
        {
            foreach (var a in actions)
                a();
        }

        [Test]
        public void Transaction_tests(
            [Values(true, false)] bool respondOnRequestBodyEnd,
            [Values(true, false)] bool deferConnectRequestBody, 
            [Values(true, false)] bool deferOnResponse, 
            [Values(true, false)] bool deferResponseBody,
            [ValueSource(typeof(InputOutput), "GetValues")] InputOutput inputOutput)
        {
            var requests = inputOutput.Requests;
            var responses = inputOutput.UserResponses;
            var expectedResponses = inputOutput.ExpectedResponses;

            var responseEnumerator = Enumerable.Empty<ResponseInfo>().Concat(responses).GetEnumerator();
            ResponseInfo currentResponse = null;

            Action<UserKayak> onResponse = kayak =>
            {
                responseEnumerator.MoveNext();
                currentResponse = responseEnumerator.Current;

                Post(() => kayak.OnResponse(currentResponse.Head), deferOnResponse);
            };

            // this is invariant
            requestCallbacker.OnRequestAction = kayak =>
            {
                Post(() => kayak.ConnectRequestBody(), deferConnectRequestBody);

                if (!respondOnRequestBodyEnd)
                    onResponse(kayak);
            };

            if (respondOnRequestBodyEnd)
                requestCallbacker.OnRequestBodyEndAction = onResponse;

            requestCallbacker.ConnectResponseBodyAction = kayak =>
            {
                var responseDataEnumerator = currentResponse.Data.GetEnumerator();
                Action send = null;
                send = () =>
                {
                    if (responseDataEnumerator.MoveNext())
                    {
                        kayak.OnResponseBodyData(responseDataEnumerator.Current);
                        Post(send, deferResponseBody);
                    }
                    else
                        kayak.OnResponseBodyEnd();
                };

                Post(send, deferResponseBody);
            };

            Drive(Interleave(GetInputActions(requests, transactionInput).GetEnumerator(), GetPostedActions().GetEnumerator()));

            requestAccumulator.AssertRequests(requests);
            responseAccumulator.AssertResponses(expectedResponses);

            Assert.That(responseAccumulator.GotEnd, Is.True);
            Assert.That(responseAccumulator.GotDispose, Is.True);
        }
    }

}
