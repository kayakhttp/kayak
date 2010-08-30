
using System.IO;
using System;
using System.Text;
namespace Kayak
{
    public static partial class Extensions
    {
        #region Common Statuses
        // might set an internal flag on some of these so that we can send some default content (i.e., 404 page)

        public static void SetStatusToOK(this IKayakServerResponse response)
        {
            response.StatusCode = 200;
            response.ReasonPhrase = "OK";
        }

        public static void SetStatusToCreated(this IKayakServerResponse response)
        {
            response.StatusCode = 201;
            response.ReasonPhrase = "Created";
        }

        public static void SetStatusToFound(this IKayakServerResponse response)
        {
            response.StatusCode = 302;
            response.ReasonPhrase = "Found";
        }

        public static void SetStatusToNotModified(this IKayakServerResponse response)
        {
            response.StatusCode = 304;
            response.ReasonPhrase = "Not Modified";
        }

        public static void SetStatusToBadRequest(this IKayakServerResponse response)
        {
            response.StatusCode = 400;
            response.ReasonPhrase = "Bad Request";
        }

        public static void SetStatusToForbidden(this IKayakServerResponse response)
        {
            response.StatusCode = 403;
            response.ReasonPhrase = "Forbidden";
        }

        public static void SetStatusToNotFound(this IKayakServerResponse response)
        {
            response.StatusCode = 404;
            response.ReasonPhrase = "Not Found";
        }

        public static void SetStatusToConflict(this IKayakServerResponse response)
        {
            response.StatusCode = 409;
            response.ReasonPhrase = "Conflict";
        }

        public static void SetStatusToInternalServerError(this IKayakServerResponse response)
        {
            response.StatusCode = 503;
            response.ReasonPhrase = "Internal Server Error";
        }

        #endregion

        public static void Redirect(this IKayakServerResponse response, string url)
        {
            response.Headers.Clear();

            response.SetStatusToFound();
            response.Headers["Location"] = url;
        }

        public static byte[] CreateHeaderBuffer(this IKayakServerResponse response)
        {
            var sb = new StringBuilder();

            sb.AppendFormat("{0} {1} {2}\r\n", response.HttpVersion, response.StatusCode, response.ReasonPhrase);
            
            var headers = response.Headers;

            if (!headers.Names.Contains("Server"))
                headers["Server"] = "Kayak";

            if (!headers.Names.Contains("Date"))
                headers["Date"] = DateTime.UtcNow.ToString();

            foreach (NameValuePair pair in headers)
                foreach (string value in pair.Values)
                    sb.AppendFormat("{0}: {1}\r\n", pair.Name, value);

            sb.Append("\r\n");

            return Encoding.ASCII.GetBytes(sb.ToString());
        }

        public static ResponseStream CreateResponseStream(this IKayakServerResponse response, ISocket stream)
        {
            var contentLength = response.Headers.GetContentLength();

            if (contentLength <= 0)
                throw new InvalidOperationException("Response does not have a valid Content-Length header value.");

            return new ResponseStream(stream, response.CreateHeaderBuffer(), contentLength);
        }
    }
}
