using System;
using HttpMachine;

namespace Kayak.Http
{
    interface IHttpRequestEventSource
    {
        void GetNextEvent(Action<HttpRequestEvent> c, Action<Exception> f);
    }

    struct HttpRequestEvent
    {
        public HttpRequestEventType Type;
        public IRequest Request;
        public bool KeepAlive;
        public ArraySegment<byte> Data;
    }

    enum HttpRequestEventType
    {
        //RequestBegan,
        RequestHeaders,
        RequestBody,
        RequestEnded,
        EndOfFile
    }

    //class HttpRequestEventSource : IHttpRequestEventSource
    //{
    //    ISocket socket;
    //    HttpParser parser;
    //    ParserHandler handler;
    //    bool gotEof;

    //    public HttpRequestEventSource(ISocket socket)
    //    {
    //        this.socket = socket;
    //        handler = new ParserHandler();
    //        parser = new HttpParser(handler);
    //    }

    //    public void GetNextEvent(Action<HttpRequestEvent> c, Action<Exception> f)
    //    {
    //        if (handler.HasState)
    //        {
    //            c(handler.GetNextState());
    //        }
    //        else
    //        {
    //            DoRead(c, f);
    //        }
    //    }

    //    void DoRead(Action<HttpRequestEvent> c, Action<Exception> f)
    //    {
    //        socket.Read(input =>
    //            {
    //                if (input.Count == 0)
    //                    gotEof = true;

    //                if (parser.Execute(input) != input.Count)
    //                    f(new Exception("Error while parsing request."));
    //                else
    //                {
    //                    if (handler.HasState)
    //                    {
    //                        c(handler.GetNextState());
    //                        // XXX cause handler to copy any data from input into new buffers
    //                    }
    //                    else if (gotEof)
    //                    {
    //                        c(new HttpRequestEvent() { Type = HttpRequestEventType.EndOfFile });
    //                    }
    //                    else
    //                        DoRead(c, f);
    //                }
    //            }, f);
    //    }
    //}
}
