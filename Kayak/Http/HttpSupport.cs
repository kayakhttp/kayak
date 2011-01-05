using System;
using System.Collections.Generic;
using System.IO;
using Coroutine;

namespace Kayak
{
    public interface IHttpSupport
    {
        ContinuationState<IDictionary<string, object>> BeginRequest(ISocket socket);
        ContinuationState<Unit> BeginResponse(ISocket socket, string status, IDictionary<string, IList<string>> response);
    }

    class HttpSupport : IHttpSupport
    {
        public ContinuationState<IDictionary<string, object>> BeginRequest(ISocket socket)
        {
            return BeginRequestInternal(socket).AsCoroutine<IDictionary<string, object>>();
        }

        public IEnumerable<object> BeginRequestInternal(ISocket socket)
        {
            var bufferHeaders = new ContinuationState<LinkedList<ArraySegment<byte>>>(socket.BufferHeaders());
            yield return bufferHeaders;

            var headerBuffers = bufferHeaders.Result;

            Dictionary<string, object> env = new Dictionary<string, object>();

            var bodyDataReadWithHeaders = headerBuffers.Last.Value;
            headerBuffers.RemoveLast();

            var headersString = headerBuffers.GetString();
            var reader = new StringReader(headersString);
            var requestLine = reader.ReadRequestLine();
            var headers = reader.ReadHeaders();

            env["Owin.RequestMethod"] = requestLine.Verb;
            env["Owin.RequestUri"] = requestLine.RequestUri;
            env["Owin.RequestHeaders"] = headers;
            env["Owin.BaseUri"] = "";
            env["Owin.RemoteEndPoint"] = socket.RemoteEndPoint;
            env["Owin.RequestBody"] = CreateReadBody(socket, bodyDataReadWithHeaders);

            // TODO provide better values
            env["Owin.ServerName"] = "";
            env["Owin.ServerPort"] = 0;
            env["Owin.UriScheme"] = "http";

            yield return env;
        }

        public Action<byte[], int, int, Action<int>, Action<Exception>> CreateReadBody(
            ISocket socket,
            ArraySegment<byte> bodyDataReadWithHeaders)
        {
            return (buffer, offset, count, r, e) =>
                {
                    if (bodyDataReadWithHeaders.Count > 0)
                    {
                        int bytesRead;

                        bytesRead = Math.Min(bodyDataReadWithHeaders.Count, count);
                        Buffer.BlockCopy(bodyDataReadWithHeaders.Array, bodyDataReadWithHeaders.Offset, buffer, offset, bytesRead);

                        if (bytesRead < bodyDataReadWithHeaders.Count)
                            bodyDataReadWithHeaders =
                                new ArraySegment<byte>(
                                    bodyDataReadWithHeaders.Array,
                                    bodyDataReadWithHeaders.Offset + bytesRead,
                                    bodyDataReadWithHeaders.Count - bytesRead);
                        else
                            bodyDataReadWithHeaders = default(ArraySegment<byte>);

                        r(bytesRead);
                    }
                    else if (socket != null)
                    {
                        socket.Read(buffer, offset, count)(r, e);
                    }
                    else
                    {
                        r(0);
                    }
                };
        }

        public ContinuationState<Unit> BeginResponse(ISocket socket, string status, IDictionary<string, IList<string>> headers)
        {
            return socket.WriteChunk(new ArraySegment<byte>(Extensions.WriteStatusLineAndHeaders(status, headers)));
        }
    }
}
