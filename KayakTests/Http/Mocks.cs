using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Kayak;
using Kayak.Http;

namespace KayakTests
{
    class MockResponse : IHttpServerResponse
    {
        public Action OnWriteContinue;
        public Action<string, IDictionary<string, string>> OnWriteHeaders;
        public Func<ArraySegment<byte>, Action, bool> OnWriteBody;
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

        public bool WriteBody(ArraySegment<byte> data, Action complete)
        {
            if (OnWriteBody != null)
                return OnWriteBody(data, complete);
            return false;
        }

        public void End()
        {
            if (OnEnd != null)
                OnEnd();
        }
    }
    /*
    class MockHttpServerDelegate : IHttpServerDelegate
    {
        public int StartCalled;
        public Action<IRequest, IResponse> Start;

        public MockRequestDelegate RequestDelegate { get; private set; }

        public MockHttpServerDelegate()
        {
            RequestDelegate = new MockRequestDelegate();
        }

        public void OnRequest(IRequest request, IResponse response)
        {
            request.Delegate = RequestDelegate;
            StartCalled++;
            if (Start != null)
                Start(request, response);
        }
    }
     */

    /*
    class MockRequestDelegate : IRequestDelegate
    {
        public int BodyCalled;
        public int ExceptionCalled;
        public int EndCalled;

        public Action BodyContinuation;

        public Func<ArraySegment<byte>, Action, bool> Body;
        public Action End;
        public Action<Exception> Error;

        public bool OnBody(ArraySegment<byte> data, Action complete)
        {
            BodyCalled++;
            BodyContinuation = complete;
            if (Body != null)
                return Body(data, complete);
            return false;
        }

        public void OnError(Exception e)
        {
            ExceptionCalled++;
            if (Error != null)
                Error(e);
        }

        public void OnEnd()
        {
            EndCalled++;
            if (End != null)
                End();
        }
    }
    */
}
