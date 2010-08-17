using System;
using System.IO;
using System.Text;
using System.Web;

namespace Kayak
{
    public struct HttpRequestLine
    {
        public string Verb;
        public string RequestUri;
        public string HttpVersion;
    }

    public static partial class Extensions
    {
        /// <summary>
        /// Parses the first line of an incoming HTTP request.
        /// </summary>
        public static void ReadHttpHeaders(this Stream stream, out HttpRequestLine requestLine, out NameValueDictionary headers)
        {
            var reader = new StreamReader(stream, Encoding.ASCII);

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

            headers = new NameValueDictionary();
            string line = null;

            while (!string.IsNullOrEmpty(line = reader.ReadLine()))
            {
                int colon = line.IndexOf(':');
                headers.Add(line.Substring(0, colon), line.Substring(colon + 1).Trim());
            }

            headers.BecomeReadOnly();
        }

        public static int GetContentLength(this NameValueDictionary headers)
        {
            int contentLength = -1;
            
            // ugly. we need to decide in one spot whether or not we're case-sensitive for all headers.
            if (headers["Content-Length"] != null)
                int.TryParse(headers["Content-Length"], out contentLength);
            else if (headers["Content-length"] != null)
                int.TryParse(headers["Content-length"], out contentLength);

            return contentLength;
        }

        const char EqualsChar = '=';
        const char AmpersandChar = '&';

        public static NameValueDictionary DecodeQueryString(this string encodedString)
        {
            return DecodeQueryString(encodedString, 0, encodedString.Length);
        }

        public static NameValueDictionary DecodeQueryString(this string encodedString,
            int charIndex, int charCount)
        {
            var result = new NameValueDictionary();
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

        static void AddNameValuePair(NameValueDictionary dict, StringBuilder name, StringBuilder value, bool hasValue)
        {
            if (name.Length > 0)
                dict.Add(HttpUtility.UrlDecode(name.ToString()), hasValue ? HttpUtility.UrlDecode(value.ToString()) : null);
        }
    }
}
