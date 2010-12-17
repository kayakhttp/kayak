using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Owin;

namespace Kayak
{
    public static partial class Extensions
    {
        public static Task<IResponse> InvokeAsync(this IApplication application, IRequest request)
        {
            var tcs = new TaskCompletionSource<IResponse>();

            application.BeginInvoke(request, iasr => 
            {
                try
                {
                    tcs.SetResult(application.EndInvoke(iasr));
                }
                catch (Exception e)
                {
                    tcs.SetException(e);
                }
            }, null);

            return tcs.Task;
        }

        public static IDisposable InvokeWithErrorHandler(this IObservable<ISocket> sockets, IApplication application)
        {
            return sockets.InvokeWithErrorHandler(application, new ErrorHandler());
        }

        public static IDisposable InvokeWithErrorHandler(this IObservable<ISocket> sockets, IApplication application, IErrorHandler errorHandler)
        {
            return sockets.Invoke(new ErrorHandlingMiddleware(application, errorHandler));
        }

        public static IDisposable Invoke(this IObservable<ISocket> sockets, IApplication application)
        {
            return sockets.Invoke(application, new HttpSupport());
        }

        public static IDisposable Invoke(this IObservable<ISocket> sockets, IApplication application, IHttpSupport http)
        {
            return sockets.Subscribe(s => 
                {
                    s.Invoke(application, http).ContinueWith(t =>
                    {
                        if (t.IsFaulted)
                        {
                            Console.WriteLine("Error while processing request.");
                            Console.Out.WriteException(t.Exception);
                        }
                    });
                });
        }

        public static Task Invoke(this ISocket socket, IApplication application, IHttpSupport http)
        {
            return socket.InvokeInternal(application, http).AsCoroutine<Unit>();
        }

        static IEnumerable<object> InvokeInternal(this ISocket socket, IApplication application, IHttpSupport http)
        {
            var beginRequest = http.BeginRequest(socket);
            yield return beginRequest;

            if (beginRequest.IsFaulted)
                throw beginRequest.Exception;

            IRequest request = beginRequest.Result;

            var invoke = application.InvokeAsync(request);

            yield return invoke;

            if (invoke.IsFaulted)
                throw invoke.Exception;

            IResponse response = invoke.Result;

            yield return http.BeginResponse(socket, response);

            foreach (var obj in response.GetBody())
            {
                var objectToWrite = obj;

                if (obj is Task)
                {
                    var task = (obj as Task).AsTaskWithValue();

                    yield return task;

                    if (task.IsFaulted)
                        throw task.Exception;

                    objectToWrite = task.Result;
                }

                socket.WriteObject(objectToWrite);
            }

            // HTTP/1.1 support might only close the connection if client wants to
            //Trace.Write("Closed connection.");
            socket.Dispose();
        }
    }
}
