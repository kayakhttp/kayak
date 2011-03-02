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
}
