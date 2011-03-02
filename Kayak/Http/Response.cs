using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace Kayak.Http
{
    class Response : IResponse
    {
        ISocket socket;
        Version version;
        bool keepAlive;

        bool wroteHeaders;
        bool ended;
        string status;
        IDictionary<string, string> headers;

        public Response(ISocket socket, Version version, bool keepAlive)
        {
            this.socket = socket;
            this.version = version;
            this.keepAlive = keepAlive;
            socket.SetNoDelay(true);
        }

        public void WriteContinue()
        {
            socket.Write(new ArraySegment<byte>(Encoding.ASCII.GetBytes("HTTP/1.1 100 Continue\r\n\r\n")), null);
        }

        public void WriteHeaders(string status, IDictionary<string, string> headers)
        {
            if (this.status != null)
                throw new InvalidOperationException("WriteHeaders was already called.");

            if (ended)
                throw new InvalidOperationException("Response was ended.");

            if (string.IsNullOrEmpty(status))
                throw new ArgumentException("status");

            this.status = status;
            this.headers = headers;
        }

        public bool WriteBody(ArraySegment<byte> data, Action continuation)
        {
            if (status == null)
                throw new Exception("Must call WriteHeaders before calling WriteBody");

            if (ended)
                throw new InvalidOperationException("Response was ended.");

            if (!wroteHeaders)
            {
                wroteHeaders = true;
                // XXX want to make sure these go out in same packet
                var head = WriteHead();
                socket.Write(new ArraySegment<byte>(head), null);
                return socket.Write(data, continuation);
            }
            else
                return socket.Write(data, continuation);
        }

        byte[] WriteHead()
        {
            // XXX don't reallocate every time
            var sb = new StringBuilder();

            sb.AppendFormat("HTTP/{0}.{1} {2}\r\n", version.Major, version.Minor, status);

            status = null;

            if (headers == null)
                headers = new Dictionary<string, string>();

            if (!headers.ContainsKey("Server"))
                headers["Server"] = "Kayak";

            if (!headers.ContainsKey("Date"))
                headers["Date"] = DateTime.UtcNow.ToString();

            // XXX allow user to modify connection behavior?
            if (version.Minor == 1 && !keepAlive)
                headers["Connection"] = "close";

            if (version.Minor == 0 && keepAlive)
                headers["Connection"] = "keep-alive";

            foreach (var pair in headers)
                foreach (var line in pair.Value.Split(new string[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries))
                    sb.AppendFormat("{0}: {1}\r\n", pair.Key, line);

            headers = null;

            sb.Append("\r\n");

            return Encoding.ASCII.GetBytes(sb.ToString());
        }

        public void End()
        {
            if (ended)
                throw new InvalidOperationException("Response was ended.");

            if (status != null && !wroteHeaders)
            {
                wroteHeaders = true;
                var head = WriteHead();
                socket.Write(new ArraySegment<byte>(head), null);
            }

            // XXX if user calls end before calling anything else, what then? 
            // need to somehow return control to transaction. dovetails
            // with pipelining. transaction should not continue until
            // client reads last response.
        }
    }
}
