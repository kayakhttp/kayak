using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Kayak.Http;

namespace Kayak.Tests.Http
{
    class TransactionInput
    {
        IHttpServerTransactionDelegate del;
        IHttpServerTransaction tx;

        public TransactionInput(IHttpServerTransaction transaction, IHttpServerTransactionDelegate del)
        {
            tx = transaction;
            this.del = del;
        }

        public void OnRequest(RequestInfo request)
        {
            // XXX determine based on request info
            var shouldKeepAlive = ShouldKeepAlive(request.Head);

            del.OnRequest(tx, request.Head, shouldKeepAlive);
        }

        bool ShouldKeepAlive(HttpRequestHead head)
        {
            if (head.Version.Major > 0 && head.Version.Minor > 0)
            {
                // HTTP/1.1
                if (head.Headers == null) return true;
                return !(head.Headers.ContainsKey("connection") && head.Headers["connection"] == "close");
            } else {
                // < HTTP/1.1
                if (head.Headers == null) return false;
                return (head.Headers.ContainsKey("connection") && head.Headers["connection"] == "keep-alive");
            }
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
}
