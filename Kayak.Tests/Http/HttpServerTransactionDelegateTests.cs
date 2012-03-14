using System;
using System.Collections.Generic;
using System.Linq;
using Kayak.Http;
using NUnit.Framework;
using System.Net;

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
        void OnResponse(HttpResponseHead head, bool hasBody);
        void OnResponseBodyData(string data);
        void OnResponseBodyError(Exception error);
        void OnResponseBodyEnd();
        void ConnectRequestBody();
        void DisconnectRequestBody();
    }

    // represents all the ways "when" user code can call kayak
    enum CallKayakWhen
    {
        OnRequest,
        AfterOnRequest,
        OnRequestBodyData,
        AfterOnRequestBodyData,
        OnRequestBodyError,
        AfterOnRequestBodyError,
        OnRequestBodyEnd,
        AfterOnRequestBodyEnd,
        OnConnectResponseBody,
        AfterOnConnectResponseBody,
        OnDisconnectResponseBody,
        AfterOnDisconnectResponseBody
    }

    // given a set of HTTP transaction situations (1.0, 1.1, body, no body, 
    // expect-continue, connection-close, keep-alive, etc), verify that user
    // receives requests, and responses are ordered correctly and transformed 
    // to account for connection behavior
    //
    // TODO
    // - define error conditions/behavior
    // - define disconnect conditions/behavior
    // - test OnResponse invoked OnRequestBodyData
    // 
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

        [Test]
        public void Adds_xff_if_none()
        {
            responseAccumulator.RemoteEndPoint = new IPEndPoint(IPAddress.Parse("1.1.1.1"), 0);

            transactionInput.OnRequest(Request.OneOneNoBody);
            transactionInput.OnRequestEnd();

            var expected = Request.OneOhNoBody;
            expected.Head.Headers["X-Forwarded-For"] = "1.1.1.1";
            requestAccumulator.AssertRequests(new[] { expected });
        }

        [Test]
        public void Appends_xff_if_any()
        {
            responseAccumulator.RemoteEndPoint = new IPEndPoint(IPAddress.Parse("1.1.1.1"), 0);

            var withXff = Request.OneOneNoBody;
            withXff.Head.Headers["X-Forwarded-For"] = "2.2.2.2";

            transactionInput.OnRequest(Request.OneOneNoBody);
            transactionInput.OnRequestEnd();

            var expected = Request.OneOhNoBody;
            expected.Head.Headers["X-Forwarded-For"] = "2.2.2.2,1.1.1.1";
            requestAccumulator.AssertRequests(new[] { expected });
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
                CallKayakWhen.OnConnectResponseBody,
                CallKayakWhen.AfterOnConnectResponseBody//,
                //CallKayakWhen.DisconnectResponseBody,
                //CallKayakWhen.AfterDisconnectResponseBody
                )] 
            CallKayakWhen connectRequestBodyWhen,
            [Values(true, false)] bool reverseConnectAndRespondOnRequest,
            [ValueSource(typeof(TransactionTestCases), "GetValues")] TransactionTestCases inputOutput)
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
                (connectRequestBodyWhen == CallKayakWhen.OnConnectResponseBody
                || connectRequestBodyWhen == CallKayakWhen.AfterOnConnectResponseBody
                || connectRequestBodyWhen == CallKayakWhen.OnDisconnectResponseBody
                || connectRequestBodyWhen == CallKayakWhen.AfterOnDisconnectResponseBody)
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
                kayak.OnResponse(currentResponse.Head, currentResponse.Data != null);
            };

            var continueWillBeSuppressed = (respondWhen == CallKayakWhen.OnRequest && connectRequestBodyWhen == CallKayakWhen.AfterOnRequest)
                    || connectRequestBodyWhen == CallKayakWhen.OnConnectResponseBody
                    || connectRequestBodyWhen == CallKayakWhen.AfterOnConnectResponseBody
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
                        .Concat(new[] { Response.OneHundredContinue })
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
                if (connectRequestBodyWhen == CallKayakWhen.OnConnectResponseBody)
                    kayak.ConnectRequestBody();

                if (connectRequestBodyWhen == CallKayakWhen.AfterOnConnectResponseBody)
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

            Drive(Interleave(GetInputActions(requests, transactionInput), GetPostedActions()));

            requestAccumulator.AssertRequests(requests);
            responseAccumulator.AssertResponses(expectedResponses);

            Assert.That(responseAccumulator.GotEnd, Is.True);
            Assert.That(responseAccumulator.GotDispose, Is.True);
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


        IEnumerable<Action> Interleave(params IEnumerable<Action>[] sources)
        {
            var enumerators = sources.Select(s => s.GetEnumerator()).ToArray();

            while (true)
            {
                bool didSomething = false;

                foreach (var source in enumerators)
                {
                    if (source.MoveNext())
                    {
                        var a = source.Current;

                        if (a != null)
                        {
                            didSomething = true;
                            yield return a;
                        }
                    }
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
    }
}
