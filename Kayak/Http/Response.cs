using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace Kayak.Http
{
    interface IHttpServerResponseInternal : IHttpServerResponse
    {
        bool KeepAlive { get; }
    }

    class Response : IHttpServerResponseInternal
    {
        Version version;
        ResponseState state;

        public bool KeepAlive { get { return state.keepAlive; } }

        string status;
        IDictionary<string, string> headers;

        IOutputStream output;

        public Response(IOutputStream output, IHttpServerRequest request, bool shouldKeepAlive)
        {
            this.version = request.Version;
            this.output = output;
            state = ResponseState.Create(request, shouldKeepAlive);
        }

        bool Write(ArraySegment<byte> data, Action continuation)
        {
            return output.Write(data, continuation);
        }

        public void WriteContinue()
        {
            state.OnWriteContinue();
            Write(new ArraySegment<byte>(Encoding.ASCII.GetBytes("HTTP/1.1 100 Continue\r\n\r\n")), null);
        }

        public void WriteHeaders(string status, IDictionary<string, string> headers)
        {
            state.EnsureWriteHeaders();

            if (string.IsNullOrEmpty(status))
                throw new ArgumentException("status");

            var spaceSplit = status.Split(' ');
            bool prohibitBody = false;
            int statusCode = 0;
            if (spaceSplit.Length > 1)
                if (int.TryParse(spaceSplit[0], out statusCode))
                {
                    if (statusCode == 204 || statusCode == 304 || (100 <= statusCode && statusCode <= 199))
                        prohibitBody = true;
                }

            state.OnWriteHeaders(prohibitBody);

            this.status = status;
            this.headers = headers;
        }

        public bool WriteBody(ArraySegment<byte> data, Action continuation)
        {
            bool renderHeaders;
            state.EnsureWriteBody(out renderHeaders);
            if (renderHeaders)
            {
                // want to make sure these go out in same packet
                // XXX can we do this better?

                var head = RenderHeaders();
                var headPlusBody = new byte[head.Length + data.Count];
                System.Buffer.BlockCopy(head, 0, headPlusBody, 0, head.Length);
                System.Buffer.BlockCopy(data.Array, data.Offset, headPlusBody, head.Length, data.Count);

                return Write(new ArraySegment<byte>(headPlusBody), continuation);
            }
            else
                return Write(data, continuation);
        }

        public void End()
        {
            bool renderHeaders = false;
            state.OnEnd(out renderHeaders);

            if (renderHeaders)
                Write(new ArraySegment<byte>(RenderHeaders()), null);

            output.End();
        }

        // XXX probably could be optimized
        byte[] RenderHeaders()
        {
            // XXX don't reallocate every time
            var sb = new StringBuilder();

            sb.AppendFormat("HTTP/{0}.{1} {2}\r\n", version.Major, version.Minor, status);

            if (headers == null)
                headers = new Dictionary<string, string>();

            if (!headers.ContainsKey("Server"))
                headers["Server"] = "Kayak";

            if (!headers.ContainsKey("Date"))
                headers["Date"] = DateTime.UtcNow.ToString();

            bool indicateConnection;
            bool indicateConnectionClose;

            bool givenConnection = headers.ContainsKey("Connection");
            bool givenConnectionClose = givenConnection && headers["Connection"] == "close";

            state.OnRenderHeaders(
                givenConnection,
                givenConnectionClose,
                out indicateConnection,
                out indicateConnectionClose);

            if (indicateConnection)
                headers["Connection"] = indicateConnectionClose ? "close" : "keep-alive";

            foreach (var pair in headers)
                foreach (var line in pair.Value.Split(new string[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries))
                    sb.AppendFormat("{0}: {1}\r\n", pair.Key, line);

            sb.Append("\r\n");

            return Encoding.ASCII.GetBytes(sb.ToString());
        }
    }
}
