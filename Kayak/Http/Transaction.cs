//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;
//using HttpMachine;
//using System.Diagnostics;

//namespace Kayak.Http
//{
//    class HttpSocketDelegate
//    {
//        int numRequests;
//        int numResponses;

//        HttpParser parser;
//        ParserEventQueue eventQueue;

//        ISocket socket;
//        Action socketContinuation;
//        Action<IRequest, IResponse> onRequest;
//        Request activeRequest;
//        Response activeResponse;
//        LinkedList<Response> responses;

//        public HttpSocketDelegate(Action<IRequest, IResponse> onRequest)
//        {
//            this.onRequest = onRequest;
//            var handler = new ParserHandler(eventQueue = new ParserEventQueue());
//            parser = new HttpParser(handler);
//            responses = new LinkedList<Response>();
//        }

//        public void Attach(ISocket socket)
//        {
//            this.socket = socket;
//            socket.OnData += socket_OnData;
//            socket.OnEnd += socket_OnEnd;
//            socket.OnError += socket_OnError;
//            socket.OnClose += socket_OnClose;
//        }

//        public void Detach(ISocket socket)
//        {
//            if (socket != this.socket) throw new Exception("Socket was not attached");

//            socket.OnData -= socket_OnData;
//            socket.OnEnd -= socket_OnEnd;
//            socket.OnError -= socket_OnError;
//            socket.OnClose -= socket_OnClose;
//            socket.Dispose();
//            this.socket = null;
//        }

//        bool OnData(ArraySegment<byte> data, Action continuation)
//        {
//            socketContinuation = continuation;

//            if (parser.Execute(data) != data.Count)
//            {
//                Trace.Write("Error while parsing request.");
//                Detach(socket);
//            }

//            return ProcessEvents(continuation);
//        }

//        bool ProcessEvents(Action continuation)
//        {
//            while (eventQueue.HasEvents)
//            {
//                var e = eventQueue.GetNextEvent();

//                switch (e.Type)
//                {
//                    case ParserEventType.RequestHeaders:
//                        numRequests++;
//                        var request = e.Request;
//                        var shouldKeepAlive = e.KeepAlive;

//                        activeRequest = new Request(request);

//                        var response = new Response(activeRequest, shouldKeepAlive, new AttachableStream(OnResponseFinished));

//                        if (activeResponse != null)
//                        {
//                            responses.AddLast(response);
//                        }
//                        else
//                        {
//                            activeResponse = response;
//                            response.Output.Attach(socket);
//                        }

//                        try
//                        {
//                            onRequest(activeRequest, response);
//                        }
//                        catch (Exception)
//                        {
//                            // XXX send 503?
//                            Detach(socket);
//                        }
//                        break;
//                    case ParserEventType.RequestBody:
//                        if (activeRequest != null)
//                        {
//                            Action c = () =>
//                                {
//                                    if (!ProcessEvents(continuation))
//                                        continuation();
//                                };
//                            if (activeRequest.RaiseOnBody(e.Data, c))
//                            {
//                                eventQueue.RebufferQueuedData();
//                                return true;
//                            }
//                        }
//                        break;
//                    case ParserEventType.RequestEnded:
//                        activeRequest.RaiseOnEnd();
//                        activeRequest = null;
//                        break;
//                }
//            }

//            return false;
//        }

//        public void OnEnd() 
//        {
//            Debug.WriteLine("Socket OnEnd.");
//            OnData(default(ArraySegment<byte>), null);

//            if (responses.Count > 0)
//                responses.Last.Value.IsLast = true;
//            else if (activeResponse != null)
//                activeResponse.IsLast = true;
//        }

//        public void OnClose() 
//        {
//            Debug.WriteLine("Socket OnClose.");
//            //Console.WriteLine("Connection " + (socket as KayakSocket).id + ": Transaction finished. " + numRequests + " req, " + numResponses + " res");
//            Detach(socket);
//        }

//        void OnResponseFinished()
//        {
//            numResponses++;
//            Debug.WriteLine("OnResponseFinished.");
//            activeResponse.Output.Detach(socket);
//            var last = activeResponse.IsLast;
//            activeResponse = null;

//            if (last)
//            {
//                Debug.WriteLine("Last response, ending socket.");
//                socket.End();
//            }
//            else if (responses.Count > 0)
//            {
//                Debug.WriteLine("Attaching socket to next response.");
//                activeResponse = responses.First.Value;
//                responses.RemoveFirst();
//                activeResponse.Output.Attach(socket);
//            }
//            else
//            {
//                Debug.WriteLine("No pending responses, will attach next.");
//            }
//        }

//        public void OnError(Exception e)
//        {
//            //requestDelegate.OnError(request, e);
//            Debug.WriteLine("Error on socket.");
//            e.PrintStacktrace();
//            Detach(socket);
//        }


//        #region Socket Event Boilerplate

//        void socket_OnClose(object sender, EventArgs e)
//        {
//            OnClose();
//        }

//        void socket_OnError(object sender, ExceptionEventArgs e)
//        {
//            OnError(e.Exception);
//        }

//        void socket_OnEnd(object sender, EventArgs e)
//        {
//            OnEnd();
//        }

//        void socket_OnData(object sender, DataEventArgs e)
//        {
//            e.WillInvokeContinuation = OnData(e.Data, e.Continuation);
//        }

//        #endregion
//    }
//}
