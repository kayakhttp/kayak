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
        StringBuilder headerBuffer;

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
            headerBuffer = new StringBuilder();

            headerBuffer.AppendFormat("HTTP/{0}.{1} {2}\r\n", version.Major, version.Minor, status);

            if (!headers.ContainsKey("Server"))
                headers["Server"] = "Kayak";

            if (!headers.ContainsKey("Date"))
                headers["Date"] = DateTime.UtcNow.ToString();

            if (version.Minor == 1 && !keepAlive)
                headers["Connection"] = "close";

            if (version.Minor == 0 && keepAlive)
                headers["Connection"] = "keep-alive";

            foreach (var pair in headers)
                foreach (var line in pair.Value.Split(new string[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries))
                    headerBuffer.AppendFormat("{0}: {1}\r\n", pair.Key, line);

            headerBuffer.Append("\r\n");
        }

        public void WriteBody(ArraySegment<byte> data, Action continuation)
        {
            if (headerBuffer == null)
                throw new Exception("Must call WriteHeaders before calling WriteBody");

            if (headerBuffer.Length > 0)
            {
                var buf = new byte[data.Count];
                Buffer.BlockCopy(data.Array, data.Offset, buf, 0, data.Count);

                WriteHead();
                Write(new ArraySegment<byte>(buf), continuation);
            }
            else
                Write(data, continuation);
        }

        void WriteHead()
        {
            var headerBuf = Encoding.UTF8.GetBytes(headerBuffer.ToString());
            headerBuffer.Length = 0; // XXX reclaim this memory?
            Debug.WriteLine("Writing headers");
            Write(new ArraySegment<byte>(headerBuf), null);
        }

        void Write(ArraySegment<byte> data, Action continuation)
        {
            Debug.WriteLine("Writing " + data.Count + " bytes");
            socket.Write(data, continuation);
        }

        public void End()
        {
            if (headerBuffer == null)
                throw new Exception("Must call WriteHeaders before calling WriteBody");

            if (headerBuffer.Length > 0)
                WriteHead();
        }
    }
}
