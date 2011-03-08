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
        int numRequests;
        int numResponses;

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
                        numRequests++;
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
            //Console.WriteLine("Connection " + (socket as KayakSocket).id + ": Transaction finished. " + numRequests + " req, " + numResponses + " res");
            socket.Dispose();
        }

        void OnResponseFinished()
        {
            numResponses++;
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
}
