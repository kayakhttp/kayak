using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Kayak.Core;

namespace Kayak.Framework
{
    public abstract class KayakService
    {
        public IKayakContext Context { get; internal set; }
        public IKayakServerRequest Request { get { return Context.Request; } }
        public IKayakServerResponse Response { get { return Context.Response; } }
    }

    public abstract class KayakService2
    {
        public IDictionary<object, object> Context { get; internal set; }
        public IHttpServerRequest Request { get; internal set; }
    }
}
