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
