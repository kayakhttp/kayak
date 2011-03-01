using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Kayak.Http
{
    class Request : IRequest
    {
        public string Method { get; internal set; }
        public string Uri { get; internal set; }
        public IDictionary<string, string> Headers { get; internal set; }
        public Version Version { get; internal set; }
        public IObservable<Tuple<ArraySegment<byte>, Action>> Body { get; internal set; }
    }
}
