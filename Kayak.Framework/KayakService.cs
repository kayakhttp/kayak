using System.Collections.Generic;
using Owin;

namespace Kayak.Framework
{
    public abstract class KayakService
    {
        //public IDictionary<string, object> Context { get; internal set; }
        public IRequest Request { get; internal set; }
    }
}
