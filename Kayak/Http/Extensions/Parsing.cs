using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Kayak
{
    /// <summary>
    /// Represents the first line of an HTTP request. Used when constructing a `KayakRequest`.
    /// </summary>
    public struct HttpRequestLine
    {
        /// <summary>
        /// The verb component of the request line (e.g., GET, POST, etc).
        /// </summary>
        public string Verb;
        /// <summary>
        /// The request URI component of the request line (e.g., /path/and?query=string).
        /// </summary>
        public string RequestUri;

        /// <summary>
        /// The HTTP version component of the request line (e.g., HTTP/1.0).
        /// </summary>
        public string HttpVersion;
    }

    public static partial class Extensions
    {
        internal static HttpRequestLine ReadRequestLine(this TextReader reader)
        {
            string statusLine = reader.ReadLine();

            if (string.IsNullOrEmpty(statusLine))
                throw new Exception("Could not parse request status.");

            var tokens = statusLine.Split(' ').Where(s => !string.IsNullOrEmpty(s)).ToArray();

            if (tokens.Length != 3 && tokens.Length != 2)
                throw new Exception("Expected 2 or 3 tokens in request line.");

            return new HttpRequestLine()
                {
                    Verb = tokens[0],
                    RequestUri = tokens[1],
                    HttpVersion = tokens.Length == 3 ? tokens[2] : "HTTP/1.0"
                };
        }

        public static IDictionary<string, IEnumerable<string>> ReadHeaders(this TextReader reader)
        {
            var headers = new Dictionary<string, IEnumerable<string>>();
            string line = null;

            while (!string.IsNullOrEmpty(line = reader.ReadLine()))
            {
                int colon = line.IndexOf(':');
                headers.Add(line.Substring(0, colon), new string[] { line.Substring(colon + 1).Trim() });
            }

            return headers;
        }

        public static byte[] WriteStatusLineAndHeaders(string status, IDictionary<string, IEnumerable<string>> headers)
        {
            var sb = new StringBuilder();

            sb.AppendFormat("HTTP/1.0 {0}\r\n", status);

            if (!headers.ContainsKey("Server"))
                headers["Server"] = new string[] { "Kayak" };

            if (!headers.ContainsKey("Date"))
                headers["Date"] = new string[] { DateTime.UtcNow.ToString() };

            foreach (var pair in headers)
                foreach (var line in pair.Value)
                sb.AppendFormat("{0}: {1}\r\n", pair.Key, line);

            sb.Append("\r\n");

            return Encoding.UTF8.GetBytes(sb.ToString());
        }
    }
}
