using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Kayak.Http
{
    interface IHeaderRenderer
    {
        void Render(IDataConsumer consumer, HttpResponseHead head);
    }

    class HttpResponseHeaderRenderer : IHeaderRenderer
    {
        public void Render(IDataConsumer consumer, HttpResponseHead head)
        {
            var status = head.Status;
            var headers = head.Headers;

            // XXX don't reallocate every time
            var sb = new StringBuilder();

            sb.AppendFormat("HTTP/1.1 {0}\r\n", status);

            if (headers == null)
                headers = new Dictionary<string, string>();

            if (!headers.ContainsKey("Server"))
                headers["Server"] = "Kayak";

            if (!headers.ContainsKey("Date"))
                headers["Date"] = DateTime.UtcNow.ToString();

            foreach (var pair in headers)
                foreach (var line in pair.Value.Split(new string[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries))
                    sb.AppendFormat("{0}: {1}\r\n", pair.Key, line);

            sb.Append("\r\n");

            consumer.OnData(new ArraySegment<byte>(Encoding.ASCII.GetBytes(sb.ToString())), null);
        }
    }
}
