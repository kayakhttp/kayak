using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace Kayak
{
    public static partial class Extensions
    {
        public static IObservable<IKayakContext> ToContexts(this IObservable<ISocket> sockets)
        {
            return Observable.CreateWithDisposable<IKayakContext>(o => sockets.Subscribe(s =>
                    {
                        s.CreateContext().Subscribe(c =>
                            {
                                o.OnNext(c);
                            },
                            e =>
                            {
                                Console.WriteLine("Exception while creating context!");
                                Console.Out.WriteException(e);
                            });
                    },
                    e =>
                    {
                        o.OnError(e);
                    },
                    () =>
                    {
                        o.OnCompleted();
                    })
            );
        }

        public static IObservable<IKayakContext> CreateContext(this ISocket socket)
        {
            if (socket == null)
                throw new ArgumentNullException("socket");

            return CreateContextInternal(socket).AsCoroutine<IKayakContext>();
        }

        static IEnumerable<object> CreateContextInternal(ISocket socket)
        {
            var stream = socket.GetStream();

            KayakServerRequest request = null;
            yield return stream.CreateRequest().Do(r => request = r);

            var response = new KayakServerResponse(stream);

            yield return new KayakContext(socket, request, response);
        }

        public static void End(this IKayakContext context)
        {
            EndInternal(context).AsCoroutine<Unit>().Subscribe(u => { }, e => 
            {
                Console.WriteLine("Exception while ending context!");
                Console.Out.WriteException(e);
            });
        }

        static IEnumerable<object> EndInternal(IKayakContext context)
        {
            var stream = context.Socket.GetStream();

            if (context.Response.Headers.GetContentLength() <= 0)
                yield return stream.WriteAsync(context.Response.CreateHeaderBuffer());

            stream.Flush(); // should be a no-op but you never know...

            stream.Close();
            stream.Dispose();

            // close connection, we only support HTTP/1.0
            context.Socket.Dispose();

            Console.WriteLine("[{0}] {1} {2} {3} : {4} {5} {6}", DateTime.Now,
                context.Request.Verb, context.Request.Path, context.Request.HttpVersion,
                context.Response.HttpVersion, context.Response.StatusCode, context.Response.ReasonPhrase);
        }
    }
}
