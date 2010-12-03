using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Web;
using Owin;

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
        /// <summary>
        /// Returns the value of the Content-Length header, or -1 if there is no Content-Length header or its value 
        /// can't be parsed.
        /// </summary>
        public static int GetContentLength(this IDictionary<string, IEnumerable<string>> headers)
        {
            int contentLength = -1;
            
            // ugly. we need to decide in one spot whether or not we're case-sensitive for all headers.
            if (headers.ContainsKey("Content-Length"))
                int.TryParse(headers["Content-Length"].First(), out contentLength);
            else if (headers.ContainsKey("Content-length"))
                int.TryParse(headers["Content-length"].First(), out contentLength);
            else if (headers.ContainsKey("content-length"))
                int.TryParse(headers["content-length"].First(), out contentLength);

            return contentLength;
        }

        public static void SetContentLength(this IDictionary<string, IEnumerable<string>> headers, int value)
        {
            headers["Content-Length"] = new string[] { value.ToString() };
        }

        public static string GetPath(this IRequest request)
        {
            return GetPath(request.Uri);
        }

        public static string GetPath(string uri)
        {
            int question = uri.IndexOf('?');
            return HttpUtility.UrlDecode(question >= 0 ? uri.Substring(0, question) : uri);
        }

        public static IDictionary<string, string> GetQueryString(this IRequest request)
        {
            return GetQueryString(request.Uri);
        }

        public static IDictionary<string, string> GetQueryString(string uri)
        {
            int question = uri.IndexOf('?');
            return question >= 0 ?
                uri.ParseQueryString(question + 1, uri.Length - question - 1) :
                new Dictionary<string, string>();
        }

        const char EqualsChar = '=';
        const char AmpersandChar = '&';

        internal static IDictionary<string, string> ParseQueryString(this string encodedString)
        {
            return ParseQueryString(encodedString, 0, encodedString.Length);
        }

        internal static IDictionary<string, string> ParseQueryString(this string encodedString,
            int charIndex, int charCount)
        {
            var result = new Dictionary<string, string>();
            var name = new StringBuilder();
            var value = new StringBuilder();
            var hasValue = false;
            var encounteredEqualsChar = false;

            for (int i = charIndex; i < charIndex + charCount; i++)
            {
                char c = encodedString[i];
                switch (c)
                {
                    case EqualsChar:
                        encounteredEqualsChar = true;
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

            if (encounteredEqualsChar)
                // last pair
                AddNameValuePair(result, name, value, hasValue);

            return result;
        }

        static void AddNameValuePair(IDictionary<string, string> dict, StringBuilder name, StringBuilder value, bool hasValue)
        {
            if (name.Length > 0)
                dict.Add(HttpUtility.UrlDecode(name.ToString()), hasValue ? HttpUtility.UrlDecode(value.ToString()) : null);
        }

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

        public static byte[] WriteStatusLineAndHeaders(this IResponse response)
        {
            var sb = new StringBuilder();

            sb.AppendFormat("HTTP/1.0 {0}\r\n", response.Status);

            var headers = response.Headers;

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
