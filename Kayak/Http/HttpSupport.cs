using System;
using System.Collections.Generic;
using System.Linq;
using Owin;
using System.Threading.Tasks;

namespace Kayak.Http
{
    public interface IHttpSupport
    {
        Task<IRequest> BeginRequest(ISocket socket);
        Task BeginResponse(ISocket socket, IResponse response);
    }

    class HttpSupport : IHttpSupport
    {
        public Task<IRequest> BeginRequest(ISocket socket)
        {
            return BufferHeaders(socket).ContinueWith(t => KayakRequest.CreateRequest(socket, t.Result));
        }

        public Task BeginResponse(ISocket socket, IResponse response)
        {
            return socket.WriteAll(new ArraySegment<byte>(response.WriteStatusLineAndHeaders()));
        }

        static int MaxHeaderLength = 1024 * 10;
        static int BufferSize = 1024 * 2;

        /// <summary>
        /// Buffers HTTP headers from a socket. The last ArraySegment in the list is any
        /// data beyond headers which was read, which may be of zero length.
        /// </summary>
        static internal Task<LinkedList<ArraySegment<byte>>> BufferHeaders(ISocket socket)
        {
            return BufferHeadersInternal(socket).AsCoroutine<LinkedList<ArraySegment<byte>>>();
        }

        static IEnumerable<object> BufferHeadersInternal(ISocket socket)
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

                Trace.Write("About to read header chunk.");
                var read = socket.Read(buffer, bufferPosition, buffer.Length - bufferPosition);
                yield return read;

                if (read.Exception != null)
                    throw read.Exception;

                bytesRead = read.Result;

                Trace.Write("Read {0} bytes.", bytesRead);

                result.AddLast(new ArraySegment<byte>(buffer, bufferPosition, bytesRead));

                if (bytesRead == 0)
                    break;

                bufferPosition += bytesRead;
                totalBytesRead += bytesRead;

                // TODO: would be nice to have a state machine and only parse once. for now
                // let's just hope to catch it on the first go-round.
                var bodyDataPosition = IndexOfAfterCRLFCRLF(result);

                if (bodyDataPosition != -1)
                {
                    Console.WriteLine("Read " + totalBytesRead + ", body starts at " + bodyDataPosition);
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

        static internal int IndexOfAfterCRLFCRLF(IEnumerable<ArraySegment<byte>> buffers)
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
    }
}
