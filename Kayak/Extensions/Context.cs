using System;
using System.Collections.Generic;
using System.Linq;

namespace Kayak
{
    public static partial class Extensions
    {
        public static IObservable<IKayakContext> ToContexts(this IObservable<ISocket> sockets)
        {
            return sockets.ToContexts(s => s.BeginContext());
        }

        public static IObservable<IKayakContext> ToContexts(this IObservable<ISocket> sockets, 
            Func<ISocket, IObservable<IKayakContext>> transform)
        {
            return sockets.SelectMany(transform);
        }

        public static IObservable<IKayakContext> BeginContext(this ISocket socket)
        {
            if (socket == null)
                throw new ArgumentNullException("socket");

            return BeginContextInternal(socket).AsCoroutine<IKayakContext>();
        }

        static IEnumerable<object> BeginContextInternal(ISocket socket)
        {
            KayakServerRequest request = null;
            yield return socket.CreateRequest().Do(r => request = r);

            var response = new KayakServerResponse(socket);

            yield return new KayakContext(socket, request, response);
        }

        public static void End(this IKayakContext context)
        {
            // TODO this is wrong.
            // maybe some internal property, (!context.Response.Body.DataWritten)
            context.End(context.Response.Headers.GetContentLength() <= 0);
        }

        public static void End(this IKayakContext context, bool writeHeaders)
        {
            EndInternal(context, writeHeaders).AsCoroutine<Unit>().Subscribe(u => { }, e => 
            {
                Console.Error.WriteLine("Exception while ending context!");
                Console.Error.WriteException(e);
            });
        }

        static IEnumerable<object> EndInternal(IKayakContext context, bool writeHeaders)
        {
            if (writeHeaders)
            {
                var headers = context.Response.CreateHeaderBuffer();
                yield return context.Socket.Write(headers, 0, headers.Length);
            }

            // close connection, we only support HTTP/1.0
            context.Socket.Dispose();

            Console.WriteLine("[{0}] {1} {2} {3} : {4} {5} {6}", DateTime.Now,
                context.Request.Verb, context.Request.Path, context.Request.HttpVersion,
                context.Response.HttpVersion, context.Response.StatusCode, context.Response.ReasonPhrase);
        }
    }
}
