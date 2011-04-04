using System;
using System.Collections.Generic;

namespace Kayak.Http
{
    class Request : IRequest
    {
        public string Method { get; internal set; }
        public string Uri { get; internal set; }
        public IDictionary<string, string> Headers { get; internal set; }
        public Version Version { get; internal set; }

        public event EventHandler<DataEventArgs> OnBody;
        public event EventHandler OnEnd;

        internal bool RaiseOnBody(ArraySegment<byte> data, Action continuation)
        {
            if (OnBody != null)
            {
                var eventArgs = new DataEventArgs()
                {
                    Data = data,
                    Continuation = continuation
                };

                OnBody(this, eventArgs);

                return eventArgs.WillInvokeContinuation;
            }

            return false;
        }

        internal void RaiseOnEnd()
        {
            if (OnEnd != null)
                OnEnd(this, EventArgs.Empty);
        }
    }
}
