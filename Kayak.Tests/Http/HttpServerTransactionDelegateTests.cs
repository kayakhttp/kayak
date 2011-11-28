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

    public class RequestInfo
    {
        public HttpRequestHead Head;
        public IEnumerable<string> Data;
        public Exception Exception;
    }

    public class ResponseInfo
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

                public static RequestInfo OneOneNoBody = new RequestInfo()
                {
                    Head = new HttpRequestHead()
                    {
                        Version = new Version(1, 1),
                        Headers = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase) 
                        {
                            { "X-Foo", "bar" }
                        }
                    }
                };

                public static RequestInfo OneOneExpectContinueWithBody = new RequestInfo()
                {
                    Head = new HttpRequestHead()
                    {
                        Version = new Version(1, 1),
                        Headers = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase) 
                        {
                            { "Expect", "100-continue" }
                        }
                    },
                    Data = new[] { "hello ", "world!" }
                };
            }

            public class Response
            {
                public static ResponseInfo OneHundredContinue = new ResponseInfo()
                {
                    Head = new HttpResponseHead()
                    {
                        Status = "100 Continue"
                    }
                };

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

                yield return new InputOutput()
                {
                    Name = "1.1 single request response",
                    Requests = new[] { Request.OneOneNoBody },
                    UserResponses = new[] { Response.TwoHundredOKWithBody },
                    ExpectedResponses = new[] { Response.TwoHundredOKWithBody }
                };

                yield return new InputOutput()
                {
                    Name = "1.1 two request response",
                    Requests = new[] { Request.OneOneNoBody, Request.OneOneNoBody },
                    UserResponses = new[] { Response.TwoHundredOKWithBody, Response.TwoHundredOKWithBody },
                    ExpectedResponses = new[] { Response.TwoHundredOKWithBody, Response.TwoHundredOKWithBody }
                };

                yield return new InputOutput()
                {
                    Name = "1.1 request with body response with body",
                    Requests = new[] { Request.OneOneExpectContinueWithBody },
                    UserResponses = new[] { Response.TwoHundredOKWithBody },
                    ExpectedResponses = new[] { Response.TwoHundredOKWithBody }
                };
            }
        }

        IEnumerable<Action> GetInputActions(IEnumerable<RequestInfo> requests, TransactionInput txInput)
        {
            foreach (var request in requests)
            {
                yield return () => transactionInput.OnRequest(request);

                if (request.Data != null)
                    foreach (var chunk in request.Data)
                        yield return () => transactionInput.OnRequestData(chunk);

                yield return () => transactionInput.OnRequestEnd();

            }

            yield return () => transactionInput.OnEnd();
            yield return () => transactionInput.OnClose();
        }

        void Post(Action action)
        {
            postedActions.Enqueue(action);
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

        public enum CallKayakWhen
        {
            OnRequest,
            AfterOnRequest,
            OnRequestBodyData,
            AfterOnRequestBodyData,
            OnRequestBodyError,
            AfterOnRequestBodyError,
            OnRequestBodyEnd,
            AfterOnRequestBodyEnd,
            ConnectResponseBody,
            AfterConnectResponseBody,
            DisconnectResponseBody,
            AfterDisconnectResponseBody
        }

        [Test]
        public void Transaction_tests(
            [Values(
                CallKayakWhen.OnRequest, 
                CallKayakWhen.AfterOnRequest,
                //CallKayakWhen.OnRequestBodyData,
                //CallKayakWhen.AfterOnRequestBodyData,
                CallKayakWhen.OnRequestBodyEnd,
                CallKayakWhen.AfterOnRequestBodyEnd//,
                //CallKayakWhen.OnRequestBodyError,
                //CallKayakWhen.AfterOnRequestBodyError
                )]
            CallKayakWhen respondWhen,

            [Values(
                CallKayakWhen.OnRequest,
                CallKayakWhen.AfterOnRequest,
                CallKayakWhen.ConnectResponseBody,
                CallKayakWhen.AfterConnectResponseBody//,
                //CallKayakWhen.DisconnectResponseBody,
                //CallKayakWhen.AfterDisconnectResponseBody
                )] 
            CallKayakWhen connectRequestBodyWhen,
            [Values(true, false)] bool reverseConnectAndRespondOnRequest,
            [ValueSource(typeof(InputOutput), "GetValues")] InputOutput inputOutput)
        {
            // some permutations are impossible. for example, it's impossible to ConnectRequestBody
            // OnConnectResponseBody if OnResponse is invoked during an OnRequestBody event...neither would ever happen.
            //
            // it's easier to blacklist impossible ones than to construct a whitelist of possible ones:
            if (
                (respondWhen == CallKayakWhen.OnRequestBodyData
                || respondWhen == CallKayakWhen.AfterOnRequestBodyData
                || respondWhen == CallKayakWhen.OnRequestBodyError
                || respondWhen == CallKayakWhen.AfterOnRequestBodyError
                || respondWhen == CallKayakWhen.OnRequestBodyEnd
                || respondWhen == CallKayakWhen.AfterOnRequestBodyEnd)
                &&
                (connectRequestBodyWhen == CallKayakWhen.ConnectResponseBody
                || connectRequestBodyWhen == CallKayakWhen.AfterConnectResponseBody
                || connectRequestBodyWhen == CallKayakWhen.DisconnectResponseBody
                || connectRequestBodyWhen == CallKayakWhen.AfterDisconnectResponseBody)
                )
                Assert.Pass("Permutation being tested is not possible."); 

            var requests = inputOutput.Requests;
            var responses = inputOutput.UserResponses;
            var expectedResponses = inputOutput.ExpectedResponses;

            var numResponded = 0;
            var responseEnumerator = Enumerable.Empty<ResponseInfo>().Concat(responses).GetEnumerator();
            ResponseInfo currentResponse = null;

            Action<UserKayak> onResponse = kayak =>
            {
                responseEnumerator.MoveNext();
                currentResponse = responseEnumerator.Current;
                numResponded++;
                kayak.OnResponse(currentResponse.Head);
            };

            var continueWillBeSuppressed = (respondWhen == CallKayakWhen.OnRequest && connectRequestBodyWhen == CallKayakWhen.AfterOnRequest)
                    || connectRequestBodyWhen == CallKayakWhen.ConnectResponseBody
                    || connectRequestBodyWhen == CallKayakWhen.AfterConnectResponseBody
                    || (reverseConnectAndRespondOnRequest &&
                    (
                        // a couple special cases where reversing the order of the calls will modify the behavior
                        (respondWhen == CallKayakWhen.OnRequest && connectRequestBodyWhen == CallKayakWhen.OnRequest)
                        || (respondWhen == CallKayakWhen.AfterOnRequest && connectRequestBodyWhen == CallKayakWhen.AfterOnRequest)
                    ))
                    ;

            requestCallbacker.OnRequestAction = (kayak, head) =>
            {
                var expectContinue = head.IsContinueExpected();

                if (expectContinue && !continueWillBeSuppressed) { 
                    expectedResponses = expectedResponses
                        .Take(numResponded)
                        .Concat(new[] { InputOutput.Response.OneHundredContinue })
                        .Concat(expectedResponses.Skip(numResponded));
                }

                if (reverseConnectAndRespondOnRequest)
                {
                    if (respondWhen == CallKayakWhen.OnRequest)
                        onResponse(kayak);

                    if (respondWhen == CallKayakWhen.AfterOnRequest)
                        Post(() => onResponse(kayak));

                    if (connectRequestBodyWhen == CallKayakWhen.OnRequest)
                        kayak.ConnectRequestBody();

                    if (connectRequestBodyWhen == CallKayakWhen.AfterOnRequest)
                        Post(() => kayak.ConnectRequestBody());
                }
                else
                {
                    if (connectRequestBodyWhen == CallKayakWhen.OnRequest)
                        kayak.ConnectRequestBody();

                    if (connectRequestBodyWhen == CallKayakWhen.AfterOnRequest)
                        Post(() => kayak.ConnectRequestBody());

                    if (respondWhen == CallKayakWhen.OnRequest)
                        onResponse(kayak);

                    if (respondWhen == CallKayakWhen.AfterOnRequest)
                        Post(() => onResponse(kayak));
                }
            };

            requestCallbacker.OnRequestBodyEndAction = kayak =>
            {
                if (respondWhen == CallKayakWhen.OnRequestBodyEnd)
                    onResponse(kayak);

                if (respondWhen == CallKayakWhen.AfterOnRequestBodyEnd)
                    Post(() => onResponse(kayak));
            };

            requestCallbacker.ConnectResponseBodyAction = kayak =>
            {
                if (connectRequestBodyWhen == CallKayakWhen.ConnectResponseBody)
                    kayak.ConnectRequestBody();

                if (connectRequestBodyWhen == CallKayakWhen.AfterConnectResponseBody)
                    Post(() => kayak.ConnectRequestBody());

                // XXX test sync also
                var responseDataEnumerator = currentResponse.Data.GetEnumerator();
                Action send = null;
                send = () =>
                {
                    if (responseDataEnumerator.MoveNext())
                    {
                        kayak.OnResponseBodyData(responseDataEnumerator.Current);
                        Post(send);
                    }
                    else
                        kayak.OnResponseBodyEnd();
                };

                Post(send);
            };

            Drive(Interleave(GetInputActions(requests, transactionInput).GetEnumerator(), GetPostedActions().GetEnumerator()));

            requestAccumulator.AssertRequests(requests);
            responseAccumulator.AssertResponses(expectedResponses);

            Assert.That(responseAccumulator.GotEnd, Is.True);
            Assert.That(responseAccumulator.GotDispose, Is.True);
        }
    }
}
