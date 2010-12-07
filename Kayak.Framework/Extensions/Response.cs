using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Owin;

namespace Kayak.Framework
{

    //public class BufferedResponse : KayakResponse
    //{
    //    LinkedList<ArraySegment<byte>> buffers;

    //    public void SetContentLength()
    //    {
    //        var cl = 0;
    //        foreach (var b in buffers)
    //            cl += b.Count;
    //        if (cl > 0)
    //            Headers.SetContentLength(cl);
    //    }

    //    public void Add(ArraySegment<byte> buffer)
    //    {
    //        if (buffers == null)
    //            buffers = new LinkedList<ArraySegment<byte>>();

    //        buffers.AddLast(buffer);
    //    }

    //    public void Add(byte[] buffer)
    //    {
    //        Add(new ArraySegment<byte>(buffer));
    //    }

    //    public void Add(string s)
    //    {
    //        Add(new ArraySegment<byte>(Encoding.UTF8.GetBytes(s)));
    //    }

    //    IEnumerable<object> EnumerateBody()
    //    {
    //        if (buffers == null)
    //            yield break;

    //        foreach (var b in buffers)
    //            yield return new ArraySegment<byte>[] { b }.ToObservable();

    //        if (buffers.Count == 0)
    //            buffers = null;
    //    }
    //}

    public static partial class Extensions
    {
        #region Common Statuses
        
        ///// <summary>
        ///// Sets the response status to 200 OK.
        ///// </summary>
        //public static void SetStatusToOK(this IKayakServerResponse response)
        //{
        //    response.StatusCode = 200;
        //    response.ReasonPhrase = "OK";
        //}

        ///// <summary>
        ///// Sets the response status to 201 Created.
        ///// </summary>
        //public static void SetStatusToCreated(this IKayakServerResponse response)
        //{
        //    response.StatusCode = 201;
        //    response.ReasonPhrase = "Created";
        //}

    //    /// <summary>
    //    /// Sets the response status to 302 Found.
    //    /// </summary>
    //    public static void SetStatusToFound(this IKayakServerResponse response)
    //    {
    //        response.StatusCode = 302;
    //        response.ReasonPhrase = "Found";
    //    }

    //    /// <summary>
    //    /// Sets the response status to 304 Not Modified.
    //    /// </summary>
    //    public static void SetStatusToNotModified(this IKayakServerResponse response)
    //    {
    //        response.StatusCode = 304;
    //        response.ReasonPhrase = "Not Modified";
    //    }

    //    /// <summary>
    //    /// Sets the response status to 400 Bad Request.
    //    /// </summary>
    //    public static void SetStatusToBadRequest(this IKayakServerResponse response)
    //    {
    //        response.StatusCode = 400;
    //        response.ReasonPhrase = "Bad Request";
    //    }

    //    /// <summary>
    //    /// Sets the response status to 403 Forbidden.
    //    /// </summary>
    //    public static void SetStatusToForbidden(this IKayakServerResponse response)
    //    {
    //        response.StatusCode = 403;
    //        response.ReasonPhrase = "Forbidden";
    //    }

    //    /// <summary>
    //    /// Sets the response status to 404 Not Found.
    //    /// </summary>
    //    public static void SetStatusToNotFound(this IKayakServerResponse response)
    //    {
    //        response.StatusCode = 404;
    //        response.ReasonPhrase = "Not Found";
    //    }

    //    /// <summary>
    //    /// Sets the response status to 409 Conflict.
    //    /// </summary>
    //    public static void SetStatusToConflict(this IKayakServerResponse response)
    //    {
    //        response.StatusCode = 409;
    //        response.ReasonPhrase = "Conflict";
    //    }

    //    /// <summary>
    //    /// Sets the response status to 503 Internal Server Error.
    //    /// </summary>
    //    public static void SetStatusToInternalServerError(this IKayakServerResponse response)
    //    {
    //        response.StatusCode = 503;
    //        response.ReasonPhrase = "Internal Server Error";
    //    }

        #endregion

    //    /// <summary>
    //    /// Sets the response status to 302 Found, and the Location header to the given URL.
    //    /// </summary>
    //    public static void Redirect(this IKayakServerResponse response, string url)
    //    {
    //        response.SetStatusToFound();
    //        response.Headers["Location"] = url;
    //    }

    //    /// <summary>
    //    /// Returns an observable which, upon subscription, writes the given `string` to the response.
    //    /// </summary>
    //    public static IObservable<Unit> Write(this IKayakServerResponse response, string str)
    //    {
    //        return response.Write(str, Encoding.UTF8);
    //    }

    //    /// <summary>
    //    /// Returns an observable which, upon subscription, writes the given `string` to the response using the given `Encoding`.
    //    /// </summary>
    //    public static IObservable<Unit> Write(this IKayakServerResponse response, string str, Encoding enc)
    //    {
    //        return response.Write(enc.GetBytes(str));
    //    }

    //    /// <summary>
    //    /// Returns an observable which, upon subscription, writes the entirety of the given buffer to the response.
    //    /// </summary>
    //    public static IObservable<Unit> Write(this IKayakServerResponse response, byte[] buffer)
    //    {
    //        return response.Write(buffer, 0, buffer.Length);
    //    }

    //    /// <summary>
    //    /// Returns an observable which, upon subscription, writes the given byte array segment to the response.
    //    /// </summary>
    //    public static IObservable<Unit> Write(this IKayakServerResponse response, ArraySegment<byte> buffer)
    //    {
    //        return response.Write(buffer.Array, buffer.Offset, buffer.Count);
    //    }

    //    internal static byte[] WriteStatusAndHeaders(this IKayakServerResponse response)
    //    {
    //        var sb = new StringBuilder();

    //        sb.AppendFormat("{0} {1} {2}\r\n", response.HttpVersion, response.StatusCode, response.ReasonPhrase);

    //        var headers = response.Headers;

    //        if (!headers.ContainsKey("Server"))
    //            headers["Server"] = "Kayak";

    //        if (!headers.ContainsKey("Date"))
    //            headers["Date"] = DateTime.UtcNow.ToString();

    //        foreach (var pair in headers)
    //            sb.AppendFormat("{0}: {1}\r\n", pair.Key, pair.Value);

    //        sb.Append("\r\n");

    //        return Encoding.UTF8.GetBytes(sb.ToString());
    //    }
    }
}
