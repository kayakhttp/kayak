using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Kayak.Http
{
    struct ParserEvent
    {
        public ParserEventType Type;
        public IRequest Request;
        public bool KeepAlive;
        public bool Rebuffered;
        public ArraySegment<byte> Data;
    }

    enum ParserEventType
    {
        RequestHeaders,
        RequestBody,
        RequestEnded
    }

    class ParserEventQueue : IParserDelegate
    {
        List<ParserEvent> events;

        public bool HasEvents { get { return events.Count > 0; } }

        public ParserEvent GetNextEvent()
        {
            var e = events[0];
            events.RemoveAt(0);
            return e;
        }

        public void RebufferQueuedData()
        {
            for (int i = 0; i < events.Count; i++)
            {
                var e = events[i];
                if (e.Type == ParserEventType.RequestBody && !e.Rebuffered)
                {
                    var dest = new byte[e.Data.Count];
                    Buffer.BlockCopy(e.Data.Array, e.Data.Offset, dest, 0, e.Data.Count);
                    events[i] = new ParserEvent()
                        {
                            Type = ParserEventType.RequestBody,
                            Data = new ArraySegment<byte>(dest),
                            Rebuffered = true
                        };
                }
            }
        }

        public ParserEventQueue()
        {
            events = new List<ParserEvent>();
        }

        public void OnRequestBegan(IRequest request, bool shouldKeepAlive)
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
