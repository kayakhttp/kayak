using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Kayak;
using Kayak.Http;

namespace KayakTests
{
    class MockResponse : IResponse
    {
        public Action OnWriteContinue;
        public Action<string, IDictionary<string, string>> OnWriteHeaders;
        public Action<ArraySegment<byte>, Action> OnWriteBody;
        public Action OnEnd;

        public void WriteContinue()
        {
            if (OnWriteContinue != null)
                OnWriteContinue();
        }

        public void WriteHeaders(string status, IDictionary<string, string> headers)
        {
            if (OnWriteHeaders != null)
                OnWriteHeaders(status, headers);
        }

        public void WriteBody(ArraySegment<byte> data, Action complete)
        {
            if (OnWriteBody != null)
                OnWriteBody(data, complete);
        }

        public void End()
        {
            if (OnEnd != null)
                OnEnd();
        }
    }

    class MockRequestDelegate : IRequestDelegate
    {
        public int StartCalled;
        public int BodyCalled;
        public int ExceptionCalled;
        public int EndCalled;

        public Action BodyContinuation;

        public Action<IRequest, IResponse> Start;
        public Func<ArraySegment<byte>, Action, bool> Body;
        public Action End;
        public Action<Exception> Error;

        public void OnStart(IRequest request, IResponse response)
        {
            StartCalled++;
            if (Start != null)
                Start(request, response);
        }

        public bool OnBody(IRequest request, ArraySegment<byte> data, Action complete)
        {
            BodyCalled++;
            BodyContinuation = complete;
            if (Body != null)
                return Body(data, complete);
            return false;
        }

        public void OnError(IRequest request, Exception e)
        {
            ExceptionCalled++;
            if (Error != null)
                Error(e);
        }

        public void OnEnd(IRequest request)
        {
            EndCalled++;
            if (End != null)
                End();
        }
    }

    class MockRequestEventSource : IHttpRequestEventSource
    {
        IEnumerator<HttpRequestEvent> states;

        public static MockRequestEventSource FromRequests(params TestRequest[] requests)
        {
            return FromRequests((IEnumerable<TestRequest>)requests);
        }

        public static MockRequestEventSource FromRequests(IEnumerable<TestRequest> requests)
        {
            return new MockRequestEventSource(GetEvents(requests));
        }

        public static IEnumerable<HttpRequestEvent> GetEvents(params TestRequest[] requests)
        {
            return GetEvents((IEnumerable<TestRequest>)requests);
        }

        public static IEnumerable<HttpRequestEvent> GetEvents(IEnumerable<TestRequest> requests)
        {
            foreach (var request in requests)
            {
                var start = new HttpRequestEvent()
                {
                    Type = HttpRequestEventType.RequestHeaders,
                    Request = new Request()
                    {
                        Method = request.Method,
                        Uri = request.Uri,
                        Version = request.Version,
                        Headers = request.Headers
                    },
                    KeepAlive = request.KeepAlive,
                };

                yield return start;

                // TODO fuzz
                if (request.Body != null)
                    yield return new HttpRequestEvent()
                    {
                        Type = HttpRequestEventType.RequestBody,
                        Data = new ArraySegment<byte>(request.Body)
                    };

                yield return new HttpRequestEvent()
                {
                    Type = HttpRequestEventType.RequestEnded
                };
            }
        }

        MockRequestEventSource(IEnumerable<HttpRequestEvent> states)
        {
            this.states = states.GetEnumerator();
        }

        public void GetNextEvent(Action<HttpRequestEvent> c, Action<Exception> f)
        {
            Console.WriteLine("GetNext");
            try
            {
                if (!states.MoveNext())
                {
                    states.Dispose();
                    c(new HttpRequestEvent() { Type = HttpRequestEventType.EndOfFile });
                }
                else
                {
                    Console.WriteLine("states.Current = " + states.Current);
                    c(states.Current);
                }
            }
            catch (Exception e)
            {
                f(e);
            }
        }
    }
}
