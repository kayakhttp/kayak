using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Kayak.Http
{
    struct ParserEvent
    {
        public ParserEventType Type;
        public HttpRequestHeaders Request;
        public bool KeepAlive;
        public ArraySegment<byte> Data;
    }

    enum ParserEventType
    {
        RequestHeaders,
        RequestBody,
        RequestEnded
    }

    class ParserEventQueue : IHighLevelParserDelegate
    {
        List<ParserEvent> events;

        public bool HasEvents { get { return events.Count > 0; } }

        public ParserEvent Dequeue()
        {
            var e = events[0];
            events.RemoveAt(0);
            return e;
        }

        public ParserEventQueue()
        {
            events = new List<ParserEvent>();
        }

        public void OnRequestBegan(HttpRequestHeaders request, bool shouldKeepAlive)
        {
            events.Add(new ParserEvent()
            {
                Type = ParserEventType.RequestHeaders,
                KeepAlive = shouldKeepAlive,
                Request = request,
            });
        }

        public void OnRequestBody(ArraySegment<byte> data)
        {
            events.Add(new ParserEvent()
            {
                Type = ParserEventType.RequestBody,
                Data = data
            });
        }

        public void OnRequestEnded()
        {
            events.Add(new ParserEvent()
            {
                Type = ParserEventType.RequestEnded
            });
        }
    }
}
