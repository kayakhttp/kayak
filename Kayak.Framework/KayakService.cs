using System;
using Owin;

namespace Kayak.Framework
{
    public abstract class KayakService
    {
        //public IDictionary<string, object> Context { get; internal set; }
        public IRequest Request { get; internal set; }
    }

    public class KayakServiceException : Exception
    {
        public KayakServiceException() : base("An error occurred while processing the request.") { }
        public KayakServiceException(string message) : base(message) { }
        public KayakServiceException(string message, Exception innerException) : base(message, innerException) { }
    }
}
