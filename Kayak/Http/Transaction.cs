using System;
using System.Collections.Generic;
using System.Diagnostics;
using Coroutine;

namespace Kayak.Http
{
    //static class Transaction
    //{
    //    public static void Run(IHttpRequestEventSource http, Func<Version, bool, IResponse> responseFactory, IRequestDelegate requestDelegate, Action c, Action<Exception> f)
    //    {
    //        Run(http, responseFactory, requestDelegate).AsContinuation()(c, f);
    //    }

    //    static IEnumerable<object> Run(IHttpRequestEventSource http, Func<Version, bool, IResponse> responseFactory, IRequestDelegate requestDelegate)
    //    {
    //        ResponseWrapper response = null;
    //        ContinuationState<HttpRequestEvent> stateContinuation;
    //        HttpRequestEvent httpState;
    //        bool keepAlive = false;

    //        var state = TransactionState.NewMessage;

    //        while (true)
    //        {
    //            if (state == TransactionState.Dead)
    //            {
    //                Debug.WriteLine("Transaction is dead. Done!");
    //                yield break;
    //            }

    //            Debug.WriteLine("Awaiting state...");
    //            stateContinuation = new ContinuationState<HttpRequestEvent>(http.GetNextEvent);
    //            yield return stateContinuation;

    //            httpState = stateContinuation.Result;

    //            Debug.WriteLine("Got state " + httpState.Type);

    //            if (httpState.Type == HttpRequestEventType.EndOfFile)
    //            {
    //                if (state == TransactionState.NewMessage)
    //                {
    //                    Debug.WriteLine("Expected new message, got EOF. Done!");
    //                    yield break;
    //                }
    //                else
    //                    throw new Exception("Client unexpectedly closed the connection.");
    //            }

    //            switch (state)
    //            {
    //                case TransactionState.NewMessage:
    //                    Debug.WriteLine("Entering TransactionState.NewMessage");
    //                    if (httpState.Type == HttpRequestEventType.RequestHeaders)
    //                    {
    //                        keepAlive = httpState.KeepAlive;

    //                        var expectContinue = httpState.Request.GetIsContinueExpected();

    //                        // if request is 1.0 or does not include expect 100-continue
    //                        // delegate must not send 100 continue (response should throw exception)
    //                        var prohibitContinue = httpState.Request.GetIsContinueProhibited();

    //                        // if some body data has been recieved, a call to writeContinue should not
    //                        // actually send the a 100 Continue response, but reading should continue anyway
    //                        var suppressContinue = false; // TODO inspect HttpSupport state

    //                        response = new ResponseWrapper(responseFactory(httpState.Request.Version, keepAlive), prohibitContinue, suppressContinue);

    //                        var start = new ContinuationState((c, f) => requestDelegate.OnStart(httpState.Request, response, c, f));

    //                        yield return start;
    //                        if (start.Exception != null)
    //                            throw new Exception("Exception during OnStart.", start.Exception);

    //                        state = TransactionState.MessageBegan;

    //                        // request delegate MUST send continue if client expects it.
    //                        // it will not be done automatically.
    //                        if (expectContinue && !response.Continued)
    //                        {
    //                            // TODO ensure 400-level response?
    //                            if (!response.Ended)
    //                                throw new Exception("Expected continue but did not recieve it, and response was not ended.");

    //                            state = TransactionState.Dead;
    //                            break;
    //                        }

    //                        if (!keepAlive && response.Ended)
    //                        {
    //                            state = TransactionState.Dead;
    //                            break;
    //                        }

    //                        break;
    //                    }

    //                    throw new Exception("Got unexpected state: " + httpState.Type);

    //                case TransactionState.MessageBegan:
    //                    Debug.WriteLine("Entering TransactionState.MessageBegan");
    //                    if (httpState.Type == HttpRequestEventType.RequestBody)
    //                    {
    //                        state = TransactionState.MessageBody;
    //                        goto case TransactionState.MessageBody;
    //                    }

    //                    if (httpState.Type == HttpRequestEventType.RequestEnded)
    //                    {
    //                        state = TransactionState.MessageFinishing;
    //                        goto case TransactionState.MessageFinishing;
    //                    }

    //                    throw new Exception("Got unexpected state: " + httpState.Type);

    //                case TransactionState.MessageBody:
    //                    Debug.WriteLine("Entering TransactionState.MessageBody");
    //                    if (httpState.Type == HttpRequestEventType.RequestBody)
    //                    {
    //                        var onBody = new ContinuationState((c, f) => requestDelegate.OnBody(httpState.Data, c, f));
    //                        yield return onBody;
    //                        if (onBody.Exception != null)
    //                            throw new Exception("Exception during OnBody.", onBody.Exception);

    //                        // XXX for now this logic is deferred to the request delegate:
    //                        // if 
    //                        // - no expect-continue
    //                        // - application ended response
    //                        // - "request includes request body"
    //                        // then 
    //                        //   read and discard remainder of incoming message and goto PreRequest

    //                        if (!keepAlive && response.Ended)
    //                        {
    //                            state = TransactionState.Dead;
    //                            break;
    //                        }

    //                        break;
    //                    }
    //                    else if (httpState.Type == HttpRequestEventType.RequestEnded)
    //                    {
    //                        state = TransactionState.MessageFinishing;
    //                        goto case TransactionState.MessageFinishing;
    //                    }

    //                    throw new Exception("Got unexpected state: " + httpState.Type);

    //                case TransactionState.MessageFinishing:
    //                    Debug.WriteLine("Entering TransactionState.MessageFinishing");
    //                    if (httpState.Type == HttpRequestEventType.RequestEnded)
    //                    {
    //                        var end = new ContinuationState((c, f) => requestDelegate.OnEnd(c, f));
    //                        yield return end;

    //                        if (end.Exception != null)
    //                            throw new Exception("Exception during OnEnd.", end.Exception);

    //                        if (!response.Ended)
    //                            throw new Exception("The request delegate continued from OnEnd but the response was not ended.");

    //                        state = keepAlive ? TransactionState.NewMessage : TransactionState.Dead;
    //                        break;
    //                    }
    //                    throw new Exception("Got unexpected state: " + httpState.Type);
    //                default:
    //                    throw new Exception("Unhandled state " + httpState.Type);
    //            }
    //        }
    //    }
    //}

    enum TransactionState
    {
        NewMessage,
        MessageBegan,
        MessageBody,
        MessageFinishing,
        Dead
    }

    class ResponseWrapper : IResponse
    {
        IResponse response;
        bool prohibitContinue, suppressContinue;
        public bool Continued;
        public bool Started;
        public bool Ended;

        public ResponseWrapper(IResponse response, bool prohibitContinue, bool suppressContinue)
        {
            this.response = response;
            this.prohibitContinue = prohibitContinue;

        }

        public void WriteContinue()
        {
            // XXX maybe I shouldn't be a dick about this?
            if (prohibitContinue)
                throw new Exception("Cannot write continue, no \"Expect: 100-continue\" in request or request was HTTP/1.0");

            Continued = true;

            if (suppressContinue)
                return;

            response.WriteContinue();
        }

        public void WriteHeaders(string status, IDictionary<string, string> headers)
        {
            Started = true;
            response.WriteHeaders(status, headers);
        }

        public void WriteBody(ArraySegment<byte> data, Action continuation)
        {
            response.WriteBody(data, continuation);
        }

        public void End()
        {
            Ended = true;
            response.End();
        }
    }
}
