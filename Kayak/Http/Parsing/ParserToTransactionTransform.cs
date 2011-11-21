using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HttpMachine;
using System.Diagnostics;

namespace Kayak.Http
{
    // adapts synchronous parser events to asynchronous "socket-like" events.
    // 
    // in so doing it introduces the backpressure mechanism to support the 
    // OnData event. this requires a "commit" phase after all the data 
    // currently in memory has been fed through the parser.
    // 
    // bundles data events such that if more events are queued, the next event 
    // cannot be deferred (i.e., ensures continuation is null). essentially, 
    // this makes sure that no backpressure is applied in the middle of a 
    // single read when events remain to be dealt with.
    // 
    // for example, if the server gets a packet with two requests in it, and 
    // the first request has an entity body, the user cannot expect to "delay" 
    // the processing of the next request by returning true from the OnData 
    // handler, since that request is already in memory.
    class ParserToTransactionTransform : IHighLevelParserDelegate
    {
        ParserEventQueue queue;
        IHttpServerTransaction transaction;
        IHttpServerTransactionDelegate transactionDelegate;

        public ParserToTransactionTransform(IHttpServerTransaction transaction, IHttpServerTransactionDelegate transactionDelegate)
        {
            this.transaction = transaction;
            this.transactionDelegate = transactionDelegate;
            queue = new ParserEventQueue();
        }

        public void OnRequestBegan(HttpRequestHeaders head, bool shouldKeepAlive)
        {
            queue.OnRequestBegan(head, shouldKeepAlive);
        }

        public void OnRequestBody(ArraySegment<byte> data)
        {
            queue.OnRequestBody(data);
        }

        public void OnRequestEnded()
        {
            queue.OnRequestEnded();
        }

        public bool Commit(Action continuation)
        {
            while (queue.HasEvents)
            {
                var e = queue.Dequeue();

                switch (e.Type)
                {
                    case ParserEventType.RequestHeaders:
                        transactionDelegate.OnRequest(transaction, new HttpRequestHead() { 
                            Method = e.Request.Method,
                            Uri = e.Request.Uri,
                            Path = e.Request.Path,
                            Fragment = e.Request.Fragment,
                            QueryString = e.Request.QueryString,
                            Version = e.Request.Version,
                            Headers = e.Request.Headers
                        }, e.KeepAlive);
                        break;
                    case ParserEventType.RequestBody:
                        if (!queue.HasEvents)
                            return transactionDelegate.OnRequestData(transaction, e.Data, continuation);

                        transactionDelegate.OnRequestData(transaction, e.Data, null);
                        break;
                    case ParserEventType.RequestEnded:
                        transactionDelegate.OnRequestEnd(transaction);
                        break;
                }
            }
            return false;
        }
    }
}
