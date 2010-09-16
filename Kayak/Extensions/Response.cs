using System;
using System.Text;

namespace Kayak
{
    public static partial class Extensions
    {
        #region Common Statuses
        
        /// <summary>
        /// Sets the response status to 200 OK.
        /// </summary>
        public static void SetStatusToOK(this IKayakServerResponse response)
        {
            response.StatusCode = 200;
            response.ReasonPhrase = "OK";
        }

        /// <summary>
        /// Sets the response status to 201 Created.
        /// </summary>
        public static void SetStatusToCreated(this IKayakServerResponse response)
        {
            response.StatusCode = 201;
            response.ReasonPhrase = "Created";
        }

        /// <summary>
        /// Sets the response status to 302 Found.
        /// </summary>
        public static void SetStatusToFound(this IKayakServerResponse response)
        {
            response.StatusCode = 302;
            response.ReasonPhrase = "Found";
        }

        /// <summary>
        /// Sets the response status to 304 Not Modified.
        /// </summary>
        public static void SetStatusToNotModified(this IKayakServerResponse response)
        {
            response.StatusCode = 304;
            response.ReasonPhrase = "Not Modified";
        }

        /// <summary>
        /// Sets the response status to 400 Bad Request.
        /// </summary>
        public static void SetStatusToBadRequest(this IKayakServerResponse response)
        {
            response.StatusCode = 400;
            response.ReasonPhrase = "Bad Request";
        }

        /// <summary>
        /// Sets the response status to 403 Forbidden.
        /// </summary>
        public static void SetStatusToForbidden(this IKayakServerResponse response)
        {
            response.StatusCode = 403;
            response.ReasonPhrase = "Forbidden";
        }

        /// <summary>
        /// Sets the response status to 404 Not Found.
        /// </summary>
        public static void SetStatusToNotFound(this IKayakServerResponse response)
        {
            response.StatusCode = 404;
            response.ReasonPhrase = "Not Found";
        }

        /// <summary>
        /// Sets the response status to 409 Conflict.
        /// </summary>
        public static void SetStatusToConflict(this IKayakServerResponse response)
        {
            response.StatusCode = 409;
            response.ReasonPhrase = "Conflict";
        }

        /// <summary>
        /// Sets the response status to 503 Internal Server Error.
        /// </summary>
        public static void SetStatusToInternalServerError(this IKayakServerResponse response)
        {
            response.StatusCode = 503;
            response.ReasonPhrase = "Internal Server Error";
        }

        #endregion

        /// <summary>
        /// Sets the response status to 302 Found, and the Location header to the given URL.
        /// </summary>
        public static void Redirect(this IKayakServerResponse response, string url)
        {
            response.SetStatusToFound();
            response.Headers["Location"] = url;
        }

        public static IObservable<Unit> Write(this IKayakServerResponse response, string str)
        {
            return response.Write(str, Encoding.UTF8);
        }

        public static IObservable<Unit> Write(this IKayakServerResponse response, string str, Encoding enc)
        {
            return response.Write(enc.GetBytes(str));
        }

        public static IObservable<Unit> Write(this IKayakServerResponse response, byte[] buffer)
        {
            return response.Write(buffer, 0, buffer.Length);
        }

        public static IObservable<Unit> Write(this IKayakServerResponse response, ArraySegment<byte> buffer)
        {
            return response.Write(buffer.Array, buffer.Offset, buffer.Count);
        }
    }
}
