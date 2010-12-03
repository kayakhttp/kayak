using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Owin;
using Kayak.Http;

namespace Kayak
{
    public static partial class Extensions
    {
        public static IDisposable RespondWith(this IObservable<ISocket> sockets, IApplication responder)
        {
            return sockets.RespondWith(responder, new HttpSupport(), new HttpResponderSupport(), new HttpErrorHandler());
        }

        public static IDisposable RespondWith(this IObservable<ISocket> sockets, IApplication responder, IHttpSupport http, IHttpResponderSupport responderSupport, IHttpErrorHandler errorHandler)
        {
            return sockets.Subscribe(s => responder.RespondTo(s, http, responderSupport, errorHandler).Subscribe(CreateContextObserver()));
        }

        public static IObserver<Unit> CreateContextObserver()
        {
            return Observer.Create<Unit>(
                c =>
                {
                    // TODO pull request and response out of context and log?
                },
                e =>
                {
                    Console.WriteLine("Error during context.");
                    Console.Out.WriteException(e);
                },
                () =>
                {
                });
        }

        public static IObservable<Unit> RespondTo(this IApplication responder, ISocket socket, IHttpSupport http, IHttpResponderSupport responderSupport, IHttpErrorHandler errorHandler)
        {
            return responder.RespondToInternal(socket, http, responderSupport, errorHandler).AsCoroutine<Unit>();
        }

        static IEnumerable<object> RespondToInternal(this IApplication responder, ISocket socket, IHttpSupport http, IHttpResponderSupport responderSupport, IHttpErrorHandler errorHandler)
        {
            IRequest request = null;

            yield return http.BeginRequest(socket).Do(r => request = r);

            IResponse response = null;

            yield return responderSupport.GetResponse(request, responder).Do(r => response = r);

            yield return SafeEnumerateResponse(response, socket, http, errorHandler).AsCoroutine<Unit>();
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

            Exception enumerationException = null;

            yield return HandleEnumerator(response, bodyEnumerator, socket, http, errorHandler).AsCoroutine<Unit>()
                // catch exceptions, want to make sure we dispose the enumerator
                .Do(u => { }, e => enumerationException = e, () => { }).Catch(Observable.Empty<Unit>());

            if (enumerationException != null)
            {
                Console.WriteLine("Error while enumerating response.");
                Console.Out.WriteException(enumerationException);
            }

            bodyEnumerator.Dispose();

            // HTTP/1.1 support might only close the connection if client wants to || enumerationException != null

            Trace.Write("Closed connection.");
            socket.Dispose();
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

                var observableItem = item.AsObservable();

                if (observableItem != null)
                {
                    Exception ex = null;
                    yield return observableItem.Do(i => obj = i, e => ex = e, () => { }).Catch(Observable.Empty<object>());

                    // exception during async processing?
                    if (ex != null)
                    {
                        // should only happen if a response provided by the error handler throws up.
                        // (verify this by searching for calls to the method you're reading now)
                        if (errorHandler == null)
                            throw new Exception("Observable in response body yielded exception, no error handler available. Did the response object from your error handle throw up?", ex);

                        if (!headersWritten)
                        {
                            var exceptionResponse = errorHandler.GetExceptionResponse(ex);
                            var exceptionBodyEnumerator = exceptionResponse.GetBody().GetEnumerator();

                            Exception errorException;

                            // use null error handler. we don't want to handle any errors 
                            // that occur while we're already handling an error.
                            yield return HandleEnumerator(exceptionResponse, exceptionBodyEnumerator, 
                                socket, http, null).AsCoroutine<Unit>()
                                // catch exceptions, want to dispose the enumerator
                                .Do(u => {}, e => errorException = e, () => { }).Catch(Observable.Empty<Unit>());

                            exceptionBodyEnumerator.Dispose();

                            yield break;
                        }
                        else
                            yield return errorHandler.WriteExceptionText(ex, socket);
                    }
                }

                if (!headersWritten)
                {
                    yield return http.BeginResponse(socket, response);
                    headersWritten = true;
                }

                yield return socket.WriteObject(obj);
            }
        }

    }
}
