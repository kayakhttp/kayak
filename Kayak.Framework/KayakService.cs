using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Kayak.Core;

namespace Kayak.Framework
{
    public abstract class KayakService2
    {
        public IDictionary<object, object> Context { get; internal set; }
        public IHttpServerRequest Request { get; internal set; }
    }
}
