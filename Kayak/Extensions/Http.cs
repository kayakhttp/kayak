using System.Collections.Generic;
using System.Text;
using System.Web;
using System;
using System.IO;

namespace Kayak
{
    /// <summary>
    /// Represents the first line of an HTTP request. Used when constructing a `KayakServerRequest`.
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
        public static int GetContentLength(this IDictionary<string, string> headers)
        {
            int contentLength = -1;
            
            // ugly. we need to decide in one spot whether or not we're case-sensitive for all headers.
            if (headers.ContainsKey("Content-Length"))
                int.TryParse(headers["Content-Length"], out contentLength);
            else if (headers.ContainsKey("Content-length"))
                int.TryParse(headers["Content-length"], out contentLength);

            return contentLength;
        }

        const char EqualsChar = '=';
        const char AmpersandChar = '&';

        public static IDictionary<string, string> ParseQueryString(this string encodedString)
        {
            return ParseQueryString(encodedString, 0, encodedString.Length);
        }

        public static IDictionary<string, string> ParseQueryString(this string encodedString,
            int charIndex, int charCount)
        {
            var result = new Dictionary<string, string>();
            var name = new StringBuilder();
            var value = new StringBuilder();
            var hasValue = false;

            for (int i = charIndex; i < charIndex + charCount; i++)
            {
                char c = encodedString[i];
                switch (c)
                {
                    case EqualsChar:
                        hasValue = true;
                        break;
                    case AmpersandChar:
                        // end of pair
                        AddNameValuePair(result, name, value, hasValue);

                        // reset
                        name.Length = value.Length = 0;
                        hasValue = false;
                        break;
                    default:
                        if (!hasValue) name.Append(c); else value.Append(c);
                        break;
                }
            }

            // last pair
            AddNameValuePair(result, name, value, hasValue);

            return result;
        }

        static void AddNameValuePair(IDictionary<string, string> dict, StringBuilder name, StringBuilder value, bool hasValue)
        {
            if (name.Length > 0)
                dict.Add(HttpUtility.UrlDecode(name.ToString()), hasValue ? HttpUtility.UrlDecode(value.ToString()) : null);
        }

        public static void ParseHttpHeaders(this IEnumerable<ArraySegment<byte>> buffers, out HttpRequestLine requestLine, out IDictionary<string, string> headers)
        {
            var reader = new StringReader(buffers.GetString());

            string statusLine = reader.ReadLine();

            if (string.IsNullOrEmpty(statusLine))
                throw new Exception("Could not parse request status.");

            int firstSpace = statusLine.IndexOf(' ');
            int lastSpace = statusLine.LastIndexOf(' ');

            if (firstSpace == -1 || lastSpace == -1)
                throw new Exception("Could not parse request status.");

            requestLine = new HttpRequestLine();
            requestLine.Verb = statusLine.Substring(0, firstSpace);

            bool hasVersion = lastSpace != firstSpace;

            if (hasVersion)
                requestLine.HttpVersion = statusLine.Substring(lastSpace + 1);
            else
                requestLine.HttpVersion = "HTTP/1.0";

            requestLine.RequestUri = hasVersion
                ? statusLine.Substring(firstSpace + 1, lastSpace - firstSpace - 1)
                : statusLine.Substring(firstSpace + 1);

            headers = new Dictionary<string, string>();
            string line = null;

            while (!string.IsNullOrEmpty(line = reader.ReadLine()))
            {
                int colon = line.IndexOf(':');
                headers.Add(line.Substring(0, colon), line.Substring(colon + 1).Trim());
            }
        }

        internal static byte[] ComposeHttpHeaders(this IKayakServerResponse response)
        {
            var sb = new StringBuilder();

            sb.AppendFormat("{0} {1} {2}\r\n", response.HttpVersion, response.StatusCode, response.ReasonPhrase);

            var headers = response.Headers;

            if (!headers.ContainsKey("Server"))
                headers["Server"] = "Kayak";

            if (!headers.ContainsKey("Date"))
                headers["Date"] = DateTime.UtcNow.ToString();

            foreach (var pair in headers)
                sb.AppendFormat("{0}: {1}\r\n", pair.Key, pair.Value);

            sb.Append("\r\n");

            return Encoding.UTF8.GetBytes(sb.ToString());
        }
    }
}
