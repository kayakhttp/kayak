using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Kayak.Framework
{
    public abstract class KayakService
    {
        public IKayakContext Context { get; internal set; }
        public IKayakServerRequest Request { get { return Context.Request; } }
        public IKayakServerResponse Response { get { return Context.Response; } }
    }
}
