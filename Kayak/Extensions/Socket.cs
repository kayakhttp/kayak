using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Kayak.Core;

namespace Kayak
{
    public static partial class Extensions
    {
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

        public static IObservable<Unit> WriteStatusLineAndHeaders(this ISocket socket, IHttpServerResponse response)
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

        public static IObservable<IHttpServerRequest> BeginRequest(this ISocket socket)
        {
            return socket.BufferHeaders().Select(h => KayakRequest.CreateRequest(socket, h));
        }
    }
}
