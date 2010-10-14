using System.Collections.Generic;
using Kayak.Core;

namespace Kayak.Framework
{
    public abstract class KayakService
    {
        public IDictionary<object, object> Context { get; internal set; }
        public IHttpServerRequest Request { get; internal set; }
    }
}
