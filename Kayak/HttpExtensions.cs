using System;
using System.IO;
using System.Text;

namespace Kayak
{
    struct HttpRequestLine
    {
        public string Verb;
        public string RequestUri;
        public string HttpVersion;
    }

    struct HttpStatusLine
    {
        public string HttpVersion;
        public int StatusCode;
        public string ReasonPhrase;
    }

    /// <summary>
    /// A collection of extension methods to help speak HTTP.
    /// </summary>
    static class HttpExtensions
    {
        /// <summary>
        /// Parses the first line of an incoming HTTP request.
        /// </summary>
        public static HttpRequestLine ReadHttpRequestLine(this Stream stream)
        {
            string statusLine = stream.ReadLine(Encoding.ASCII, 1024 * 4);

            if (string.IsNullOrEmpty(statusLine))
                throw new Exception("Could not parse request status.");

            int firstSpace = statusLine.IndexOf(' ');
            int lastSpace = statusLine.LastIndexOf(' ');

            if (firstSpace == -1 || lastSpace == -1)
                throw new Exception("Could not parse request status.");

            var requestLine = new HttpRequestLine();
            requestLine.Verb = statusLine.Substring(0, firstSpace);

            bool hasVersion = lastSpace != firstSpace;

            if (hasVersion)
                requestLine.HttpVersion = statusLine.Substring(lastSpace + 1);
            else
                requestLine.HttpVersion = "HTTP/1.0";

            requestLine.RequestUri = hasVersion
                ? statusLine.Substring(firstSpace + 1, lastSpace - firstSpace - 1)
                : statusLine.Substring(firstSpace + 1);

            return requestLine;
        }

        /// <summary>
        /// Parses a list of HTTP request headers, terminated by an empty line.
        /// </summary>
        public static NameValueDictionary ReadHttpHeaders(this Stream stream)
        {
            var headers = new NameValueDictionary();
            string line = null;

            while (!string.IsNullOrEmpty(line = stream.ReadLine(Encoding.ASCII, 1024 * 4)))
            {
                int colon = line.IndexOf(':');
                headers.Add(line.Substring(0, colon), line.Substring(colon + 1).Trim());
            }

            headers.BecomeReadOnly();

            return headers;
        }

        public static void WriteHttpStatusLine(this Stream stream,
            HttpStatusLine statusLine)
        {
            // e.g. HTTP/1.0 200 OK
            string line = string.Format("{0} {1} {2}\r\n", statusLine.HttpVersion, statusLine.StatusCode, statusLine.ReasonPhrase);
            byte[] statusBytes = Encoding.ASCII.GetBytes(line);
            stream.Write(statusBytes, 0, statusBytes.Length);
        }

        public static void WriteHttpHeaders(this Stream stream, NameValueDictionary headers)
        {
            /*using (*/
            StreamWriter writer = new StreamWriter(stream, Encoding.ASCII);//)
            //{
                writer.NewLine = "\r\n";

                foreach (NameValuePair pair in headers)
                    foreach (string value in pair.Values)
                        writer.WriteLine("{0}: {1}", pair.Name, value);

                writer.WriteLine();
                writer.Flush();
           // }
        }

        public static int GetContentLength(this NameValueDictionary headers)
        {
            int contentLength = -1;
            
            if (headers["Content-Length"] != null)
                int.TryParse(headers["Content-Length"], out contentLength);
            else if (headers["Content-length"] != null)
                int.TryParse(headers["Content-length"], out contentLength);

            return contentLength;
        }

        /// <summary>
        /// Synchronously reads a line terminated with \r\n off the source stream into a 
        /// string using the given Encoding. Throws if the line buffer exceeds maxLength bytes.
        /// </summary>
        public static string ReadLine(this Stream source, Encoding encoding, int maxLength)
        {
            using (var readLineBuffer = new MemoryStream())
            {
                readLineBuffer.Position = 0;

                int? last = null;

                while (true)
                {
                    if (readLineBuffer.Position == maxLength)
                        throw new Exception("Encountered a line exceeding maxLength (" + maxLength + "bytes)");

                    int b = source.ReadByte();

                    if (b == -1 || (last == 13 && b == 10))
                        break;

                    if (last.HasValue)
                        readLineBuffer.WriteByte((byte)last.Value);

                    last = b;
                }

                return encoding.GetString(readLineBuffer.GetBuffer(), 0, (int)readLineBuffer.Position);
            }
        }
    }
}
