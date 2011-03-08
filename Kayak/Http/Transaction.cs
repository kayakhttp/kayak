using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HttpMachine;
using System.Diagnostics;

namespace Kayak.Http
{
    class HttpSocketDelegate : ISocketDelegate
    {
        HttpParser parser;
        ParserEventQueue eventQueue;

        ISocket socket;
        IHttpServerDelegate del;
        Action socketContinuation;

        IRequest activeRequest;
        Response activeResponse;
        LinkedList<Response> responses;

        public HttpSocketDelegate(ISocket socket, IHttpServerDelegate del)
        {
            this.socket = socket;
            this.del = del;
            var handler = new ParserHandler();
            handler.Delegate = eventQueue = new ParserEventQueue();
            parser = new HttpParser(handler);
            responses = new LinkedList<Response>();
        }

        public bool OnData(ISocket socket, ArraySegment<byte> data, Action continuation)
        {
            socketContinuation = continuation;

            if (parser.Execute(data) != data.Count)
            {
                Trace.Write("Error while parsing request.");
                socket.Dispose();
            }

            return ProcessEvents(continuation);
        }

        bool ProcessEvents(Action continuation)
        {
            while (eventQueue.HasEvents)
            {
                var e = eventQueue.GetNextEvent();

                switch (e.Type)
                {
                    case ParserEventType.RequestHeaders:
                        var request = e.Request;
                        var shouldKeepAlive = e.KeepAlive;

                        activeRequest = request;

                        var response = new Response(request, shouldKeepAlive, OnResponseFinished);

                        if (activeResponse != null)
                        {
                            responses.AddLast(response);
                        }
                        else
                        {
                            activeResponse = response;
                            response.Socket = socket;
                        }

                        del.OnRequest(request, response);
                        break;
                    case ParserEventType.RequestBody:
                        if (activeRequest.Delegate != null)
                        {
                            Action c = () =>
                                {
                                    if (!ProcessEvents(continuation))
                                        continuation();
                                };
                            if (activeRequest.Delegate.OnBody(e.Data, c))
                            {
                                eventQueue.RebufferQueuedData();
                                return true;
                            }
                        }
                        break;
                    case ParserEventType.RequestEnded:
                        if (activeRequest.Delegate != null)
                            activeRequest.Delegate.OnEnd();
                        activeRequest = null;
                        break;
                }
            }

            return false;
        }

        public void OnEnd(ISocket socket) 
        {
            Debug.WriteLine("Socket OnEnd.");
            OnData(socket, default(ArraySegment<byte>), null);

            if (responses.Count > 0)
                responses.Last.Value.IsLast = true;
            else
                socket.End();
        }

        public void OnClose(ISocket socket) 
        {
            Debug.WriteLine("Socket OnClose.");
            socket.Dispose();
        }

        void OnResponseFinished()
        {
            Debug.WriteLine("OnResponseFinished.");
            activeResponse.Socket = null;
            var last = activeResponse.IsLast;
            activeResponse = null;

            if (last)
            {
                Debug.WriteLine("Last response, ending socket.");
                socket.End();
            }
            else if (responses.Count > 0)
            {
                Debug.WriteLine("Attaching socket to next response.");
                activeResponse = responses.First.Value;
                responses.RemoveFirst();
                activeResponse.Socket = socket;
            }
            else
            {
                Debug.WriteLine("No pending responses, will attach next.");
            }
        }

        public void OnTimeout(ISocket socket)
        {
            OnError(socket, new Exception("Socket timeout"));
        }

        public void OnError(ISocket socket, Exception e)
        {
            //requestDelegate.OnError(request, e);
            Debug.WriteLine("Error on socket.");
            e.PrintStacktrace();
            socket.Dispose();
        }

        public void OnConnected(ISocket socket) { }
    }

    /*
    
    class Transaction
    {
        enum TransactionState
        {
            NewMessage,
            MessageBegan,
            MessageBody,
            MessageFinishing,
            Dead
        }

        bool keepAlive;
        TransactionState state;
        IRequest request;
        IHttpServerDelegate serverDelegate;


        LinkedList<Response> responses;

        public Transaction(IHttpServerDelegate serverDelegate)
        {
            this.serverDelegate = serverDelegate;
            keepAlive = true;
        }

        public bool Execute(ParserEvent httpEvent, Action continuation)
        {
            if (state == TransactionState.Dead)
            {
                throw new Exception("Transaction is dead, expected no more events.");
            }

            switch (state)
            {
                case TransactionState.NewMessage:
                    Debug.WriteLine("Entering TransactionState.NewMessage");
                    if (httpEvent.Type == ParserEventType.RequestHeaders)
                    {
                        keepAlive = httpEvent.KeepAlive;
                        request = httpEvent.Request;

                        var response = responseFactory(httpEvent.Request.Version, keepAlive, ResponseEnded);

                        serverDelegate.OnRequest(httpEvent.Request, response);

                        state = TransactionState.MessageBegan;

                        break;
                    }
                    Debug.WriteLine("Got unexpected state: " + httpEvent.Type);
                    state = TransactionState.Dead;
                    break;

                case TransactionState.MessageBegan:
                    Debug.WriteLine("Entering TransactionState.MessageBegan");
                    if (httpEvent.Type == ParserEventType.RequestBody)
                    {
                        state = TransactionState.MessageBody;
                        goto case TransactionState.MessageBody;
                    }

                    if (httpEvent.Type == ParserEventType.RequestEnded)
                    {
                        state = TransactionState.MessageFinishing;
                        goto case TransactionState.MessageFinishing;
                    }
                    Debug.WriteLine("Got unexpected state: " + httpEvent.Type);
                    state = TransactionState.Dead;
                    break;

                case TransactionState.MessageBody:
                    if (httpEvent.Type == ParserEventType.RequestBody)
                    {
                        if (request.Delegate == null)
                            return false;

                        return request.Delegate.OnBody(httpEvent.Data, continuation);

                        // XXX for now this logic is deferred to the request delegate:
                        // if 
                        // - no expect-continue
                        // - application ended response
                        // - "request includes request body"
                        // then 
                        //   read and discard remainder of incoming message and goto PreRequest
                    }
                    else if (httpEvent.Type == ParserEventType.RequestEnded)
                    {
                        state = TransactionState.MessageFinishing;
                        goto case TransactionState.MessageFinishing;
                    }
                    
                    Debug.WriteLine("Got unexpected state: " + httpEvent.Type);
                    state = TransactionState.Dead;
                    break;

                case TransactionState.MessageFinishing:
                    Debug.WriteLine("Entering TransactionState.MessageFinishing");
                    if (httpEvent.Type == ParserEventType.RequestEnded)
                    {
                        if (request.Delegate != null)
                            request.Delegate.OnEnd();

                        // XXX state should be influenced by connection header sent by user.
                        // also state should not advance to NewMessage until client reads
                        // response (ensure that they're not just blasting us with requests
                        // and then closing the connection without reading)
                        state = keepAlive ? TransactionState.NewMessage : TransactionState.Dead;
                        break;
                    }
                    Debug.WriteLine("Got unexpected state: " + httpEvent.Type);
                    state = TransactionState.Dead;
                    break;

                default:
                    throw new Exception("Unhandled state " + httpEvent.Type);
            }

            return false;
        }
    }

    */
}
