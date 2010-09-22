using System;
using System.Collections.Generic;
using System.Linq;

namespace Kayak
{
    /// <summary>
    /// A simple implementation of `IKayakContext`. The `Socket`, `Request`, and `Response` objects
    /// are provided to the constructor.
    /// </summary>
    public class KayakContext : IKayakContext
    {
        public ISocket Socket { get; private set; }
        public IKayakServerRequest Request { get; private set; }
        public IKayakServerResponse Response { get; private set; }
        public IDictionary<object, object> Items { get; private set; }

        /// <summary>
        /// Constructs a new KayakContext with the provided `ISocket`, `IKayakServerRequest`, and 
        /// `IKayakServerResponse` objects.
        /// </summary>
        public KayakContext(ISocket socket, IKayakServerRequest request, IKayakServerResponse response)
        {
            Socket = socket;
            Request = request;
            Response = response;
            Items = new Dictionary<object, object>();
        }

        public static KayakContext CreateContext(ISocket socket)
        {
            var request = new KayakServerRequest(socket);
            var response = new KayakServerResponse(socket);

            return new KayakContext(socket, request, response);
        }
    }
}
