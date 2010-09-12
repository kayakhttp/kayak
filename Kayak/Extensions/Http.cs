using System;
using System.IO;
using System.Text;
using System.Web;
using System.Collections.Generic;

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
        /// The HTTP version conmponent of the request line (e.g., HTTP/1.0).
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

        public static IDictionary<string, string> DecodeQueryString(this string encodedString)
        {
            return DecodeQueryString(encodedString, 0, encodedString.Length);
        }

        public static IDictionary<string, string> DecodeQueryString(this string encodedString,
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
    }
}
