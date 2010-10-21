using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Kayak.Core;
using Kayak.Http;

namespace Kayak
{
    public interface IHttpResponderSupport
    {
        IObservable<IHttpServerResponse> GetResponse(IHttpServerRequest request, IHttpResponder responder);
    }

    public class HttpResponderSupport : IHttpResponderSupport
    {
        public IObservable<IHttpServerResponse> GetResponse(IHttpServerRequest request, IHttpResponder responder)
        {
            object responseObj = null;

            responseObj = responder.Respond(request);

            var observable = responseObj.AsObservable();

            if (observable != null)
                return observable.Select(o => AsResponse(o));

            return new IHttpServerResponse[] { AsResponse(responseObj) }.ToObservable();
        }

        public virtual IHttpServerResponse AsResponse(object responseObj)
        {
            IHttpServerResponse response = null;

            if (responseObj is IHttpServerResponse)
                response = responseObj as IHttpServerResponse;
            else if (responseObj is object[])
                response = (responseObj as object[]).ToResponse();
            else
                throw new ArgumentException("cannot convert argument " + responseObj.ToString() + " to IHttpServerResponse");

            return response;
        }
    }


    public interface IHttpErrorHandler
    {
        // called if an exception occurs before the response has begun.
        IHttpServerResponse GetExceptionResponse(Exception exception);

        // called if an exception occurs after the response has begun
        IObservable<Unit> WriteExceptionText(Exception exception, ISocket socket);
    }

    public class HttpErrorHandler : IHttpErrorHandler
    {
        public IHttpServerResponse GetExceptionResponse(Exception e)
        {
            var sw = new StringWriter();
            sw.WriteException(e);
            return new KayakResponse("503 Internal Server Error", new Dictionary<string, string>(), sw);
        }

        public IObservable<Unit> WriteExceptionText(Exception exception, ISocket socket)
        {
            throw new NotImplementedException();
        }
    }

    public static partial class Extensions
    {
        public static IDisposable RespondWith(this IObservable<ISocket> sockets, IHttpResponder responder)
        {
            return sockets.RespondWith(responder, new HttpSupport(), new HttpResponderSupport(), new HttpErrorHandler());
        }

        public static IDisposable RespondWith(this IObservable<ISocket> sockets, IHttpResponder responder, IHttpSupport http, IHttpResponderSupport responderSupport, IHttpErrorHandler errorHandler)
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

        public static IObservable<Unit> RespondTo(this IHttpResponder responder, ISocket socket, IHttpSupport http, IHttpResponderSupport responderSupport, IHttpErrorHandler errorHandler)
        {
            return responder.RespondToInternal(socket, http, responderSupport, errorHandler).AsCoroutine<Unit>();
        }

        static IEnumerable<object> RespondToInternal(this IHttpResponder responder, ISocket socket, IHttpSupport http, IHttpResponderSupport responderSupport, IHttpErrorHandler errorHandler)
        {
            IHttpServerRequest request = null;
            yield return http.BeginRequest(socket).Do(r => request = r);

            IHttpServerResponse response = null;
            yield return responderSupport.GetResponse(request, responder).Do(r => response = r);

            IEnumerator<object> bodyEnumerator = null;

            // enumerate first chunk of body. if success, write to socket.
            // if fail, provide exception response
            // enumerate second chunk. if sucess, write. if fail, write something helpful and close the connection.

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

            bodyEnumerator.Dispose();

            // HTTP/1.1 support might only close the connection if client wants to || enumerationException != null

            Trace.Write("Closed connection.");
            socket.Dispose();
        }

        static IEnumerable<object> HandleEnumerator(IHttpServerResponse response, 
            IEnumerator<object> bodyEnumerator, ISocket socket, IHttpSupport http, IHttpErrorHandler errorHandler)
        {
            var headersWritten = false;

            while (true)
            {
                var continues = bodyEnumerator.MoveNext();

                if (!continues) break;

                var item = bodyEnumerator.Current;

                if (item == null) continue;

                var observableItem = item.AsObservable();

                object obj = item;

                if (observableItem != null)
                {
                    Exception ex = null;
                    yield return observableItem.Do(i => obj = i, e => ex = e, () => { }).Catch(Observable.Empty<object>());

                    if (ex != null)
                    {
                        // should only happen if a response provided by the error handler throws up.
                        if (errorHandler == null)
                            throw new Exception("Observable in response body yielded exception, no error handler available.", ex);

                        if (!headersWritten)
                        {
                            var exceptionResponse = errorHandler.GetExceptionResponse(ex);
                            var exceptionBodyEnumerator = response.GetBody().GetEnumerator();

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

                yield return WriteObject(obj, socket);
            }
        }

        public static IObservable<Unit> WriteObject(object obj, ISocket socket)
        {
            if (obj is FileInfo)
            {
                var fileInfo = obj as FileInfo;
                return socket.WriteFile(fileInfo.Name);
            }
            else
            {
                var chunk = default(ArraySegment<byte>);

                if (obj is ArraySegment<byte>)
                    chunk = (ArraySegment<byte>)obj;
                else if (obj is string)
                    chunk = new ArraySegment<byte>(Encoding.UTF8.GetBytes(obj as string));

                return socket.WriteAll(chunk);
            }
        }
    }
}
