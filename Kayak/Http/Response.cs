using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace Kayak.Http
{
    abstract class OutgoingMessage
    {
        public bool IsLast;

        protected bool wroteHeaders;
        protected bool ended;
        ISocket socket;
        readonly Action onFinished;

        Action sendContinuation;
        LinkedList<byte[]> buffer;

        protected OutgoingMessage(Action onFinished)
        {
            this.onFinished = onFinished;
        }

        public ISocket Socket
        {
            get
            {
                return socket;
            }
            set
            {
                socket = value;
                if (socket != null)
                    Flush();
            }
        }

        protected void Flush()
        {
            while (buffer != null && buffer.Count > 0)
            {
                var first = buffer.First;
                buffer.RemoveFirst();

                var continuation = buffer.Count == 0 && sendContinuation != null ?
                    sendContinuation : null;

                if (socket.Write(new ArraySegment<byte>(buffer.First.Value), continuation))
                    return;
            }

            if (ended)
                onFinished();
        }

        protected bool WriteBody(ArraySegment<byte> data, Action continuation)
        {
            if (ended)
                throw new InvalidOperationException("Response was ended.");

            if (!wroteHeaders)
            {
                wroteHeaders = true;

                // want to make sure these go out in same packet
                // XXX can we do this better?

                var head = GetHead();
                var headPlusBody = new byte[head.Length + data.Count];
                Buffer.BlockCopy(head, 0, headPlusBody, 0, head.Length);
                Buffer.BlockCopy(data.Array, data.Offset, headPlusBody, head.Length, data.Count);

                return Send(new ArraySegment<byte>(headPlusBody), continuation);
            }
            else
                return Send(data, continuation);
        }

        protected bool Send(ArraySegment<byte> data, Action continuation)
        {
            if (socket == null)
            {
                if (buffer == null)
                    buffer = new LinkedList<byte[]>();

                var b = new byte[data.Count];
                Buffer.BlockCopy(data.Array, data.Offset, b, 0, data.Count);

                buffer.AddLast(b);

                if (continuation != null)
                {
                    sendContinuation = continuation;
                    return true;
                }

                return false;
            }
            else
            {
                // XXX possible that buffer is not empty?
                if (buffer != null && buffer.Count > 0) throw new Exception("Buffer was not empty.");

                return socket.Write(data, continuation);
            }
        }

        protected abstract byte[] GetHead();

        public void End()
        {
            if (ended)
                throw new InvalidOperationException("Response was ended.");

            ended = true;

            if (!wroteHeaders)
            {
                wroteHeaders = true;
                var head = GetHead();
                Send(new ArraySegment<byte>(head), null);
            }

            if (socket != null)
            {
                onFinished();
            }
        }
    }

    class Response : OutgoingMessage, IResponse
    {
        Version version;
        bool keepAlive;

        bool prohibitBody;
        bool expectContinue;
        bool sentContinue;

        bool sentConnectionHeader;

        string status;
        IDictionary<string, string> headers;

        public Response(IRequest request, bool keepAlive, Action onFinished)
            : base (onFinished)
        {
            this.version = request.Version;
            this.prohibitBody = request.Method == "HEAD";
            this.keepAlive = keepAlive;
            this.expectContinue = request.GetIsContinueExpected();
        }

        public void WriteContinue()
        {
            sentContinue = true;
            Send(new ArraySegment<byte>(Encoding.ASCII.GetBytes("HTTP/1.1 100 Continue\r\n\r\n")), null);
        }

        public void WriteHeaders(string status, IDictionary<string, string> headers)
        {
            if (ended)
                throw new InvalidOperationException("Response was ended.");

            if (this.status != null)
                throw new InvalidOperationException("WriteHeaders was already called.");

            if (string.IsNullOrEmpty(status))
                throw new ArgumentException("status");

            this.status = status;
            this.headers = headers;

            if (headers.ContainsKey("connection"))
            {
                sentConnectionHeader = true;
                if (headers["connection"] == "close")
                    IsLast = true;
                else
                    keepAlive = true;
            }

            var spaceSplit = status.Split(' ');
            int statusCode = 0;
            if (spaceSplit.Length > 1)
                if (int.TryParse(spaceSplit[0], out statusCode))
                {
                    if (statusCode == 204 || statusCode == 304 || (100 <= statusCode && statusCode <= 199))
                        prohibitBody = true;
                }

            if (expectContinue && !sentContinue)
                keepAlive = false;
        }

        public bool WriteBody(ArraySegment<byte> data, Action continuation)
        {
            if (this.status == null)
                throw new InvalidOperationException("Must call WriteHeaders before calling WriteBody.");
            if (prohibitBody)
                throw new InvalidOperationException("This type of response must not have a body.");

            return base.WriteBody(data, continuation);
        }

        protected override byte[] GetHead()
        {
            if (status == null)
                throw new Exception("WriteHeaders was not called, cannot generate head.");

            // XXX don't reallocate every time
            var sb = new StringBuilder();

            sb.AppendFormat("HTTP/{0}.{1} {2}\r\n", version.Major, version.Minor, status);

            if (headers == null)
                headers = new Dictionary<string, string>();

            if (!headers.ContainsKey("Server"))
                headers["Server"] = "Kayak";

            if (!headers.ContainsKey("Date"))
                headers["Date"] = DateTime.UtcNow.ToString();

            if (!sentConnectionHeader)
            {
                if (keepAlive)
                    headers["Connection"] = "keep-alive";
                else
                {
                    headers["Connection"] = "close";
                    IsLast = true;
                }
            }

            foreach (var pair in headers)
                foreach (var line in pair.Value.Split(new string[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries))
                    sb.AppendFormat("{0}: {1}\r\n", pair.Key, line);

            sb.Append("\r\n");

            return Encoding.ASCII.GetBytes(sb.ToString());
        }
    }
}
