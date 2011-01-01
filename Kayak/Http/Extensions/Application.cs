using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Coroutine;
using Owin;

namespace Kayak
{
    public delegate void
        OwinApplication(IDictionary<string, object> env,
        Action<Tuple<string, IDictionary<string, IEnumerable<string>>, IEnumerable<object>>> completed,
        Action<Exception> faulted); 

    public static partial class Extensions
    {

        //public static void InvokeOnScheduler(this IApplication application, IRequest request, TaskScheduler scheduler)
        //{
        //    var task = new Task(() =>
        //        {
                    
        //        });
        //}

        //public static IDisposable InvokeWithErrorHandler(this IObservable<ISocket> sockets, IApplication application, TaskScheduler scheduler)
        //{
        //    return sockets.InvokeWithErrorHandler(application, new ErrorHandler(), scheduler);
        //}

        //public static IDisposable InvokeWithErrorHandler(this IObservable<ISocket> sockets, IApplication application, IErrorHandler errorHandler, TaskScheduler scheduler))
        //{
        //    return sockets.Host(new ErrorHandlingMiddleware(application, errorHandler), scheduler);
        //}

        public static void Host(this IKayakServer server, OwinApplication application)
        {
            server.Host(application, null);
        }

        public static void Host(this IKayakServer server, OwinApplication application, Action<Action> trampoline)
        {
            server.Host(new HttpSupport(), application, trampoline);
        }

        public static void Host(this IKayakServer server, IHttpSupport http, OwinApplication application, Action<Action> trampoline)
        {
            server.HostInternal(http, application, trampoline).AsContinuation<object>(trampoline)
                (_ => { }, e => {
                    Console.WriteLine("Error while hosting application.");
                    Console.Out.WriteException(e);
                });
        }

        static IEnumerable<object> HostInternal(this IKayakServer server, IHttpSupport http, OwinApplication application, Action<Action> trampoline)
        {
            while (true)
            {
                var accept = new ContinuationState<ISocket>((r, e) => server.GetConnection()(r));
                yield return accept;

                if (accept.Result == null)
                    break;

                accept.Result.ProcessSocket(http, application, trampoline);
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

            var invoke = new ContinuationState<Tuple<string, IDictionary<string, IEnumerable<string>>, IEnumerable<object>>>
                ((r, e) => application(request, r, e));

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

                var write = socket.WriteObject(objectToWrite);
                yield return write;

                var foo = write.Result;
            }

            // HTTP/1.1 support might only close the connection if client wants to
            //Trace.Write("Closed connection.");
            socket.Dispose();
        }
    }
}
