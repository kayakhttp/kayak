using System;
using System.Linq;

namespace Kayak
{
    public static partial class Extensions
    {
        public static IObservable<IKayakContext> ToContexts(this IObservable<ISocket> sockets)
        {
            return sockets.Select(s => s.CreateContext());
        }

        public static IKayakContext CreateContext(this ISocket socket)
        {
            var request = new KayakServerRequest(socket);
            var response = new KayakServerResponse(socket);

            return new KayakContext(socket, request, response);
        }
    }
}
