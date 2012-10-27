using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Kayak.Http
{
    class OutputQueue : IOutputQueue
    {
        List<IOutputSegment> segments;
        
        IHttpServerTransaction transaction;
        
        public OutputQueue(IHttpServerTransaction transaction) 
        {
            
        }
        
        public IOutputSegment GetSegment() 
        {
            var segment = new OutputSegment();
            segments.Add(segment);
            return segment;
        }
        
        public void End() 
        {
            transaction.OnEnd();
        }
        
        public void Dispose() 
        {
            transaction.Dispose();
        }
        
    }
    
    class OutputData
    {
        public HttpResponseHead head;
        
        public ArraySegment<byte> data;
        public Action continuation;
        
        public bool end;
    }
    
    class OutputSink
    {
        void Drain(List<OutputData> buffer) { }
    }
    
    class OutputSegment : IOutputSegment
    {
        HttpResponseHead head;
        Kayak.OutputBuffer buffer;
        
        OutputQueue queue;
        
        #region IOutputSegment implementation
        public void OnResponse(HttpResponseHead head, IDataProducer body)
        {
            //buffer.Add(new OutputData() { head = head });
        }

        public bool OnResponseData(ArraySegment<byte> data, Action continuation)
        {
            buffer.Add(data);
            return false;
        }

        public void OnResponseError(Exception e)
        {
            throw new NotImplementedException();
        }

        public void OnResponseEnd()
        {
            throw new NotImplementedException();
        }
        #endregion
    }
    
    interface IOutputQueue : IDisposable
    {
        IOutputSegment GetSegment();
        void End();
    }
    
    interface IOutputSegment
    {
        void OnResponse(HttpResponseHead head, IDataProducer body);
    }
    
    
//    
//    interface ITransactionSegment
//    {
//        void AttachNext(ITransactionSegment next);
//        void AttachTransaction(IHttpServerTransaction transaction);
//    }
//
//    class ResponseSegment : ITransactionSegment, /*private*/ IDataConsumer
//    {
//        bool gotContinue, gotResponse, bodyFinished;
//
//        IHttpServerTransaction transaction;
//        HttpResponseHead head;
//        IDataProducer body;
//
//        ITransactionSegment next;
//
//        public void WriteContinue()
//        {
//            if (gotResponse) return;
//
//            if (gotContinue) throw new InvalidOperationException("WriteContinue was previously called.");
//            gotContinue = true;
//
//            if (transaction != null)
//                transaction.OnContinue();
//        }
//
//        public void WriteResponse(HttpResponseHead head, IDataProducer body)
//        {
//            if (gotResponse) throw new InvalidOperationException("WriteResponse was previously called.");
//            gotResponse = true;
//
//            this.head = head;
//            this.body = body;
//
//            if (transaction != null)
//                DoWriteResponse();
//        }
//
//        public void AttachNext(ITransactionSegment next)
//        {
//            this.next = next;
//
//            HandOffTransactionIfPossible();
//        }
//
//        public void AttachTransaction(IHttpServerTransaction transaction)
//        {
//            this.transaction = transaction;
//
//            if (gotContinue)
//                transaction.OnContinue();
//
//            if (gotResponse)
//                DoWriteResponse();
//        }
//
//        void DoWriteResponse()
//        {
//            transaction.OnResponse(head);
//
//            if (body != null)
//            {
//                // XXX there is no cancel.
//                body.Connect(this);
//            }
//            else
//            {
//                transaction.OnResponseEnd();
//                HandOffTransactionIfPossible();
//            }
//        }
//
//        public void OnError(Exception e)
//        {
//            transaction.Dispose();
//            transaction = null;
//            next = null;
//        }
//
//        public bool OnData(ArraySegment<byte> data, Action continuation)
//        {
//            return transaction.OnResponseData(data, continuation);
//        }
//
//        public void OnEnd()
//        {
//            transaction.OnResponseEnd();
//
//            bodyFinished = true;
//
//            HandOffTransactionIfPossible();
//        }
//
//        void HandOffTransactionIfPossible()
//        {
//            if (gotResponse && (body == null || (body != null && bodyFinished)) && transaction != null && next != null)
//            {
//                next.AttachTransaction(transaction);
//                transaction = null;
//                next = null;
//                body = null;
//            }
//        }
//    }
//
//    class EndSegment : ITransactionSegment
//    {
//        public void AttachNext(ITransactionSegment next)
//        {
//
//        }
//
//        public void AttachTransaction(IHttpServerTransaction transaction)
//        {
//            transaction.OnEnd();
//        }
//    }

}
