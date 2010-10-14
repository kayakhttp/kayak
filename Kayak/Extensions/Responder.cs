using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Kayak.Core;

namespace Kayak
{
    public static partial class Extensions
    {
        public static IDisposable RespondWith(this IObservable<ISocket> sockets, IHttpResponder responder, Func<Exception, IHttpServerResponse> getExceptionResponse)
        {
            return sockets.Subscribe(s => responder.RespondTo(s, getExceptionResponse).Subscribe(CreateContextObserver()));
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

        public static IObservable<Unit> RespondTo(this IHttpResponder responder, ISocket socket, Func<Exception, IHttpServerResponse> getExceptionResponse)
        {
            return responder.RespondToInternal(socket, getExceptionResponse).AsCoroutine<Unit>();
        }

        static IEnumerable<object> RespondToInternal(this IHttpResponder responder, ISocket socket, Func<Exception, IHttpServerResponse> getExceptionResponse)
        {
            IHttpServerRequest request = null;

            yield return socket.BeginRequest().Do(r => request = r);

            object responseObj = null;

            try
            {
                responseObj = responder.Respond(request);
            }
            catch (Exception e)
            {
                responseObj = getExceptionResponse(e);
            }

            var observable = responseObj.AsObservable();

            if (observable != null)
                yield return observable.Do(o => responseObj = o);

            IHttpServerResponse response = null;

            if (responseObj is IHttpServerResponse)
                response = responseObj as IHttpServerResponse;
            else if (responseObj is object[])
                response = (responseObj as object[]).ToResponse();

            IEnumerator<object> bodyEnumerator = null;

            // enumerate first chunk of body. if success, write to socket.
            // if fail, provide exception response
            // enumerate second chunk. if sucess, write. if fail, write something helpful and close the connection.

            try
            {
                bodyEnumerator = response.GetBody().GetEnumerator();
            }
            catch (Exception e)
            {
                response = getExceptionResponse(e);
                bodyEnumerator = response.GetBody().GetEnumerator();
            }

            var exceptionWhileEnumerating = false;
            var headersWritten = false;

            while (true)
            {
                var continues = false;
                try
                {
                    continues = bodyEnumerator.MoveNext();
                }
                catch (Exception e)
                {
                    if (exceptionWhileEnumerating) throw;

                    if (headersWritten /* && can spit out something helpful cos content type is text/* or something */)
                    {
                        // TODO capture exception, flag to write something helpful on next pass.
                        throw;
                    }

                    response = getExceptionResponse(e);
                    bodyEnumerator = response.GetBody().GetEnumerator();
                    exceptionWhileEnumerating = true;
                    continue;
                }

                if (!continues) break;

                var item = bodyEnumerator.Current;

                if (item == null) continue;

                var observableItem = item.AsObservable();

                object obj = item;

                if (observableItem != null)
                    yield return observableItem.Do(i => obj = i);

                if (obj is FileInfo)
                {
                    var fileInfo = obj as FileInfo;

                    if (!fileInfo.Exists) continue;

                    if (!headersWritten)
                    {
                        yield return socket.WriteStatusLineAndHeaders(response);
                        headersWritten = true;
                    }

                    yield return socket.WriteFile(fileInfo.Name);
                }
                else
                {
                    var chunk = default(ArraySegment<byte>);

                    if (obj is ArraySegment<byte>)
                        chunk = (ArraySegment<byte>)obj;
                    else if (obj is string)
                        chunk = new ArraySegment<byte>(Encoding.UTF8.GetBytes(obj as string));

                    if (!headersWritten)
                    {
                        yield return socket.WriteStatusLineAndHeaders(response);
                        headersWritten = true;
                    }

                    yield return socket.WriteAll(chunk);
                }
            }

            Trace.Write("Closed connection.");
            socket.Dispose();
        }
    }
}
