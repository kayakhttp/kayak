using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Kayak.Http
{
    interface ITransactionSegment
    {
        void AttachNext(ITransactionSegment next);
        void AttachTransaction(IHttpServerTransaction transaction);
    }

    class ResponseSegment : ITransactionSegment, /*private*/ IDataConsumer
    {
        bool gotContinue, gotResponse, bodyFinished;

        IHttpServerTransaction transaction;
        HttpResponseHead head;
        IDataProducer body;

        ITransactionSegment next;

        public void WriteContinue()
        {
            if (gotResponse) return;

            if (gotContinue) throw new InvalidOperationException("WriteContinue was previously called.");
            gotContinue = true;

            if (transaction != null)
                transaction.OnContinue();
        }

        public void WriteResponse(HttpResponseHead head, IDataProducer body)
        {
            if (gotResponse) throw new InvalidOperationException("WriteResponse was previously called.");
            gotResponse = true;

            this.head = head;
            this.body = body;

            if (transaction != null)
                DoWriteResponse();
        }

        public void AttachNext(ITransactionSegment next)
        {
            this.next = next;

            HandOffTransactionIfPossible();
        }

        public void AttachTransaction(IHttpServerTransaction transaction)
        {
            this.transaction = transaction;

            if (gotContinue)
                transaction.OnContinue();

            if (gotResponse)
                DoWriteResponse();
        }

        void DoWriteResponse()
        {
            transaction.OnResponse(head);

            if (body != null)
            {
                // XXX there is no cancel.
                body.Connect(this);
            }
            else
            {
                transaction.OnResponseEnd();
                HandOffTransactionIfPossible();
            }
        }

        public void OnError(Exception e)
        {
            transaction.Dispose();
            transaction = null;
            next = null;
        }

        public bool OnData(ArraySegment<byte> data, Action continuation)
        {
            return transaction.OnResponseData(data, continuation);
        }

        public void OnEnd()
        {
            transaction.OnResponseEnd();

            bodyFinished = true;

            HandOffTransactionIfPossible();
        }

        void HandOffTransactionIfPossible()
        {
            if (gotResponse && (body == null || (body != null && bodyFinished)) && transaction != null && next != null)
            {
                next.AttachTransaction(transaction);
                transaction = null;
                next = null;
                body = null;
            }
        }
    }

    class EndSegment : ITransactionSegment
    {
        public void AttachNext(ITransactionSegment next)
        {

        }

        public void AttachTransaction(IHttpServerTransaction transaction)
        {
            transaction.OnEnd();
        }
    }

}
