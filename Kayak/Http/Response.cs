using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace Kayak.Http
{
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

        public Response(IRequest request, bool keepAlive, SocketBuffer buffer)
            : base (buffer)
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

            if (headers.ContainsKey("Connection"))
            {
                sentConnectionHeader = true;
                if (headers["Connection"] == "close")
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
