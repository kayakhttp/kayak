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
    }

    public static class KayakContextExtensions
    {
        /// <summary>
        /// Transforms a sequence of `ISocket` into to a sequence of `KayakContext`. Call the `Begin` method
        /// of a context's `Request` parameter to start processing the request.
        /// </summary>
        /// <param name="sockets"></param>
        /// <returns></returns>
        public static IObservable<IKayakContext> ToContexts(this IObservable<ISocket> sockets)
        {
            return sockets.Select(socket =>
            {
                var request = new KayakServerRequest(socket);
                var response = new KayakServerResponse(socket);

                return (IKayakContext)new KayakContext(socket, request, response);
            });
        }
    }
}
