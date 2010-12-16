using System;
using System.Collections.Generic;
using System.Linq;
using Kayak.Http;
using Owin;
using System.Threading.Tasks;

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

        public static IDisposable Invoke(this IObservable<ISocket> sockets, IApplication application)
        {
            return sockets.Invoke(application, new HttpSupport(), new HttpErrorHandler());
        }

        public static IDisposable Invoke(this IObservable<ISocket> sockets, IApplication responder, IHttpSupport http, IHttpErrorHandler errorHandler)
        {
            return sockets.Subscribe(s => 
                {
                    s.Invoke(responder, http, errorHandler).ContinueWith(t =>
                    {
                        if (t.Exception != null)
                        {
                            Console.WriteLine("Error while processing request.");
                            Console.Out.WriteException(t.Exception);
                        }
                    });
                });
        }

        public static Task Invoke(this ISocket socket, IApplication application, IHttpSupport http, IHttpErrorHandler errorHandler)
        {
            return socket.InvokeInternal(application, http, errorHandler).AsCoroutine<Unit>();
        }

        static IEnumerable<object> InvokeInternal(this ISocket socket, IApplication application, IHttpSupport http, IHttpErrorHandler errorHandler)
        {
            IRequest request = null;
            Exception exception = null;

            var beginRequest = http.BeginRequest(socket);
            yield return beginRequest;

            if (beginRequest.IsFaulted)
                throw beginRequest.Exception;

            request = beginRequest.Result;

            IResponse response = null;

            var invoke = application.InvokeAsync(request);

            yield return invoke;

            if (invoke.IsFaulted)
            {
                response = errorHandler.GetExceptionResponse(invoke.Exception);
                errorHandler = null;
            }
            else
                response = invoke.Result;

            yield return SafeEnumerateResponse(response, socket, http, errorHandler).AsCoroutine<Unit>();

            // HTTP/1.1 support might only close the connection if client wants to || enumerationException != null

            Trace.Write("Closed connection.");
            socket.Dispose();
        }

        // this method will not throw.
        static IEnumerable<object> SafeEnumerateResponse(IResponse response, ISocket socket, IHttpSupport http, IHttpErrorHandler errorHandler)
        {
            IEnumerator<object> bodyEnumerator = null;

            // enumerate first chunk of body. if success, write to socket.
            // if fail, provide exception response
            // enumerate remaining chunks. if success, write. if fail, write something helpful and close the connection.

            Exception ex = null;

            try
            {
                bodyEnumerator = SafeResponseEnumerator.Create(response, socket, errorHandler);
            }
            catch (Exception e)
            {
                ex = e;
            }

            if (ex != null)
            {
                response = errorHandler.GetExceptionResponse(ex);
                bodyEnumerator = response.GetBody().GetEnumerator();
                errorHandler = null;
            }

            var handleEnumerator = HandleEnumerator(response, bodyEnumerator, socket, http, errorHandler).AsCoroutine<Unit>();

            yield return handleEnumerator;

            bodyEnumerator.Dispose();

            if (handleEnumerator.IsFaulted)
                throw handleEnumerator.Exception;
        }

        static IEnumerable<object> HandleEnumerator(IResponse response, 
            IEnumerator<object> bodyEnumerator, ISocket socket, IHttpSupport http, IHttpErrorHandler errorHandler)
        {
            var headersWritten = false;

            while (true)
            {
                var continues = bodyEnumerator.MoveNext();

                if (!continues) break;

                var item = bodyEnumerator.Current;

                if (item == null) continue;

                object obj = item;

                Task<object> taskWithValue = item is Task ? (item as Task).AsTaskWithValue() : null;

                if (taskWithValue != null)
                {
                    yield return taskWithValue;

                    obj = taskWithValue.Result;

                    // exception during async processing?
                    if (taskWithValue.IsFaulted)
                    {
                        // should only happen if a response provided by the error handler throws up.
                        // (verify this by searching for calls to the method you're reading now)
                        if (errorHandler == null)
                            throw new Exception("Observable in response body yielded exception, no error handler available. Did the response object from your error handle throw up?", taskWithValue.Exception);

                        if (!headersWritten)
                        {
                            var exceptionResponse = errorHandler.GetExceptionResponse(taskWithValue.Exception);

                            // use null error handler. we don't want to handle any errors 
                            // that occur while we're already handling an error.
                            yield return SafeEnumerateResponse(exceptionResponse, socket, http, null).AsCoroutine<Unit>();
                            yield break;
                        }
                        else
                            yield return errorHandler.WriteExceptionText(taskWithValue.Exception, socket);
                    }
                }

                if (!headersWritten)
                {
                    yield return http.BeginResponse(socket, response);
                    headersWritten = true;
                }

                yield return socket.WriteObject(obj);
            }

            if (!headersWritten)
            {
                yield return http.BeginResponse(socket, response);
                headersWritten = true;
            }
        }

    }
}
