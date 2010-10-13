using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Kayak.Core;
using System.IO;

namespace Kayak
{
    public static partial class Extensions
    {
        public static IDisposable RespondWith(this IObservable<ISocket> sockets, IHttpResponder responder)
        {
            return sockets.Subscribe(s => s.RespondWith(responder).Subscribe(CreateContextObserver()));
        }

        public static IObserver<Dictionary<object, object>> CreateContextObserver()
        {
            return Observer.Create<Dictionary<object, object>>(
                c =>
                {
                    // TODO pull request and response out of context and log
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

        public static IObservable<Dictionary<object, object>> RespondWith(this ISocket socket, IHttpResponder responder)
        {
            return socket.RespondWithInternal(responder, null).AsCoroutine<Dictionary<object, object>>();
        }

        static IEnumerable<object> RespondWithInternal(this ISocket socket, IHttpResponder responder, Func<Exception, IHttpServerResponse> getExceptionResponse)
        {
            LinkedList<ArraySegment<byte>> headerBuffers = null;

            yield return socket.BufferHeaders().Do(h => headerBuffers = h);

            IHttpServerRequest request = socket.CreateRequest(headerBuffers);

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
                        yield return socket.WriteHeaders(response);
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
                        yield return socket.WriteHeaders(response);
                        headersWritten = true;
                    }

                    yield return socket.WriteAll(chunk);
                }
            }

            Trace.Write("Closed connection.");
            socket.Dispose();
        }

        public static IObservable<Unit> WriteHeaders(this ISocket socket, IHttpServerResponse response)
        {
            return socket.WriteAll(new ArraySegment<byte>(response.WriteStatusLineAndHeaders()));
        }

        public static IObservable<Unit> WriteAll(this ISocket socket, ArraySegment<byte> chunk)
        {
            return socket.WriteAllInternal(chunk).AsCoroutine<Unit>();
        }
        static IEnumerable<object> WriteAllInternal(this ISocket socket, ArraySegment<byte> chunk)
        {
            int bytesWritten = 0;

            while (bytesWritten < chunk.Count)
            {
                yield return socket.Write(chunk.Array, chunk.Offset + bytesWritten, chunk.Count - bytesWritten)
                    .Do(n => bytesWritten += n);
            }
        }

        static int MaxHeaderLength = 1024 * 10;
        static int BufferSize = 1024 * 2;

        /// <summary>
        /// Buffers HTTP headers from a socket. The last ArraySegment in the list is any
        /// data beyond headers which was read, which may be of zero length.
        /// </summary>
        public static IObservable<LinkedList<ArraySegment<byte>>> BufferHeaders(this ISocket socket)
        {
            return socket.BufferHeadersInternal().AsCoroutine<LinkedList<ArraySegment<byte>>>();
        }

        static IEnumerable<object> BufferHeadersInternal(this ISocket socket)
        {
            LinkedList<ArraySegment<byte>> result = new LinkedList<ArraySegment<byte>>();

            int bufferPosition = 0, totalBytesRead = 0;
            byte[] buffer = null;

            while (true)
            {
                if (buffer == null || bufferPosition > BufferSize / 2)
                {
                    buffer = new byte[BufferSize];
                    bufferPosition = 0;
                }

                int bytesRead = 0;

                //Trace.Write("About to read header chunk.");
                yield return socket.Read(buffer, bufferPosition, buffer.Length - bufferPosition).Do(n => bytesRead = n);
                //Trace.Write("Read {0} bytes.", bytesRead);

                result.AddLast(new ArraySegment<byte>(buffer, bufferPosition, bytesRead));

                if (bytesRead == 0)
                    break;

                bufferPosition += bytesRead;
                totalBytesRead += bytesRead;

                // TODO: would be nice to have a state machine and only parse once. for now
                // let's just hope to catch it on the first go-round.
                var bodyDataPosition = result.IndexOfAfterCRLFCRLF();

                if (bodyDataPosition != -1)
                {
                    //Console.WriteLine("Read " + totalBytesRead + ", body starts at " + bodyDataPosition);
                    var last = result.Last.Value;
                    result.RemoveLast();
                    var overlapLength = totalBytesRead - bodyDataPosition;
                    result.AddLast(new ArraySegment<byte>(last.Array, last.Offset, last.Count - overlapLength));
                    result.AddLast(new ArraySegment<byte>(last.Array, last.Offset + last.Count - overlapLength, overlapLength));
                    break;
                }

                // TODO test this
                if (totalBytesRead > MaxHeaderLength)
                    throw new Exception("Request headers data exceeds max header length.");
            }

            yield return result;
        }

        internal static int IndexOfAfterCRLFCRLF(this IEnumerable<ArraySegment<byte>> buffers)
        {
            Queue<byte> lastFour = new Queue<byte>(4);

            int i = 0;
            foreach (var b in buffers.GetBytes())
            {
                if (lastFour.Count == 4)
                    lastFour.Dequeue();

                lastFour.Enqueue(b);

                if (lastFour.ElementAt(0) == 0x0d && lastFour.ElementAt(1) == 0x0a &&
                    lastFour.ElementAt(2) == 0x0d && lastFour.ElementAt(3) == 0x0a)
                    return i + 1;

                i++;
            }

            return -1;
        }

        public static IHttpServerRequest CreateRequest(this ISocket socket, LinkedList<ArraySegment<byte>> headerBuffers)
        {
            var bodyDataReadWithHeaders = headerBuffers.Last.Value;
            headerBuffers.RemoveLast();

            IHttpServerRequest request = null;

            var context = new Dictionary<object, object>();

            var headersString = headerBuffers.GetString();
            using (var reader = new StringReader(headersString))
                request = new KayakRequest(socket, reader.ReadRequestLine(), reader.ReadHeaders(), context, bodyDataReadWithHeaders);

            return request;
        }
    }
}
