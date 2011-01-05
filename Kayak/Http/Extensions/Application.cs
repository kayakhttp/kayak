using System;
using System.Collections.Generic;
using Coroutine;
using System.IO;

namespace Kayak
{
    public delegate void
        OwinApplication(IDictionary<string, object> env,
        Action<string, IDictionary<string, IList<string>>, IEnumerable<object>> completed,
        Action<Exception> faulted); 

    public static partial class Extensions
    {
        public static void Host(this IKayakServer server, OwinApplication application)
        {
            server.Host(application, null);
        }

        public static void Host(this IKayakServer server, OwinApplication application, Action<Action> trampoline)
        {
            server.HostInternal(application, trampoline).AsContinuation<object>(trampoline)
                (_ => { }, e => {
                    Console.WriteLine("Error while hosting application.");
                    Console.Out.WriteException(e);
                });
        }

        static IEnumerable<object> HostInternal(this IKayakServer server, OwinApplication application, Action<Action> trampoline)
        {
            while (true)
            {
                var accept = new ContinuationState<ISocket>((r, e) => server.GetConnection()(r));
                yield return accept;

                if (accept.Result == null)
                    break;

                accept.Result.ProcessSocket(new HttpSupport(), application, trampoline);
            }
        }

        public static void ProcessSocket(this ISocket socket, IHttpSupport http, OwinApplication application, Action<Action> trampoline)
        {
            socket.ProcessSocketInternal(http, application).AsContinuation<object>(trampoline)
                (_ => { }, e =>
                {
                    Console.WriteLine("Error while processing request.");
                    Console.Out.WriteException(e);
                });
        }

        static IEnumerable<object> ProcessSocketInternal(this ISocket socket, IHttpSupport http, OwinApplication application)
        {
            var beginRequest = http.BeginRequest(socket);
            yield return beginRequest;

            var request = beginRequest.Result;

            var invoke = new ContinuationState<Tuple<string, IDictionary<string, IList<string>>, IEnumerable<object>>>
                ((r, e) => 
                    application(request, 
                                (s,h,b) => 
                                    r(new Tuple<string,IDictionary<string,IList<string>>,IEnumerable<object>>(s, h, b)), 
                                e));

            yield return invoke;

            var response = invoke.Result;

            yield return http.BeginResponse(socket, response.Item1, response.Item2);

            foreach (var obj in response.Item3)
            {
                var objectToWrite = obj;

                if (obj is Action<Action<object>, Action<Exception>>)
                {
                    var cs = new ContinuationState<object>(obj as Action<Action<object>, Action<Exception>>);

                    yield return cs;

                    objectToWrite = cs.Result;
                }

                if (objectToWrite is FileInfo)
                {
                    yield return new ContinuationState(socket.WriteFile((objectToWrite as FileInfo).Name));
                    continue;
                }

                var chunk = default(ArraySegment<byte>);

                if (obj is ArraySegment<byte>)
                    chunk = (ArraySegment<byte>)obj;
                else if (obj is byte[])
                    chunk = new ArraySegment<byte>(obj as byte[]);
                else
                    continue;
                    //throw new ArgumentException("Invalid object of type " + obj.GetType() + " '" + obj.ToString() + "'");

                var write = socket.WriteChunk(chunk);
                yield return write;

                // TODO enumerate to completion
                if (write.Exception != null)
                    throw write.Exception;
            }

            socket.Dispose();
        }
    }
}
