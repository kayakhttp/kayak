using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Kayak
{
    public class KayakContext : IKayakContext
    {
        public ISocket Socket { get; private set; }
        public IKayakServerRequest Request { get; private set; }
        public IKayakServerResponse Response { get; private set; }
        public IDictionary<object, object> Items { get; private set; }

        public KayakContext(ISocket socket, IKayakServerRequest request, IKayakServerResponse response)
        {
            Socket = socket;
            Request = request;
            Response = response;
            Items = new Dictionary<object, object>();
        }
    }
}
