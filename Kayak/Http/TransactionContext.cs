using System;
using Kayak.Http;
using System.Collections.Generic;

namespace Kayak
{
    class TransactionContext : IHttpResponseDelegate
    {
        IOutputQueue outputQueue;
        
        public DataSubject RequestBody;
        
        bool expectContinue, ignoreResponseBody, shouldKeepAlive;
        bool gotConnectRequestBody, gotOnResponse;

        public TransactionContext(IOutputQueue outputQueue,
            bool expectContinue, bool ignoreResponseBody, bool shouldKeepAlive)
        {
            this.outputQueue = outputQueue;
            
            RequestBody = new DataSubject(ConnectRequestBody);

            this.expectContinue = expectContinue;
            this.ignoreResponseBody = ignoreResponseBody;
            this.shouldKeepAlive = shouldKeepAlive;
        }

        Action<bool> ConnectRequestBody()
        {
            if (gotConnectRequestBody) 
                throw new InvalidOperationException("Request body was already connected.");

            gotConnectRequestBody = true;

            if (expectContinue && !gotOnResponse)
                outputQueue.GetSegment().OnContinue();

            return (bool completed) => {
                if (!completed) {
                    outputQueue.End();
                }
            };
        }

        public void OnResponse(HttpResponseHead head, IDataProducer body)
        {
            if (gotOnResponse)
                throw new InvalidOperationException("OnResponse was already called.");
            
            gotOnResponse = true;
                
            if (!shouldKeepAlive)
            {
                if (head.Headers == null)
                    head.Headers = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);

                head.Headers["Connection"] = "close";
            }

            var segment = outputQueue.GetSegment();
            segment.OnResponse(head, body);
            
            if (!ignoreResponseBody)
                body.Connect(new SegmentConsumer(segment));
        }
    }
}

