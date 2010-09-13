using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;

namespace Kayak
{
    /// <summary>
    /// Represents a socket which supports asynchronous IO operations.
    /// </summary>
    public interface ISocket : IDisposable
    {
        /// <summary>
        /// The IP end point of the connected peer.
        /// </summary>
        IPEndPoint RemoteEndPoint { get; }

        /// <summary>
        /// Returns an observable which, upon subscription, begins an asynchronous write
        /// operation. When the operation completes, the observable yields the number of
        /// bytes written and completes.
        /// </summary>
        IObservable<int> Write(byte[] buffer, int offset, int count);

        /// <summary>
        /// Returns an observable which, upon subscription, begins copying a file
        /// to the socket. When the copy operation completes, the observable yields
        /// the number of bytes written and completes.
        /// </summary>
        IObservable<int> WriteFile(string file, int offset, int count);

        /// <summary>
        /// Returns an observable which, upon subscription, begins an asynchronous read
        /// operation. When the operation completes, the observable yields the number of
        /// bytes read and completes.
        /// </summary>
        IObservable<int> Read(byte[] buffer, int offset, int count);
    }

    /// <summary>
    /// Represents a single HTTP transaction.
    /// </summary>
    public interface IKayakContext
    {
        /// <summary>
        /// The socket over which an HTTP transaction is taking place.
        /// </summary>
        ISocket Socket { get; }

        /// <summary>
        /// An object which represents the request data of an HTTP transaction.
        /// </summary>
        IKayakServerRequest Request { get; }

        /// <summary>
        /// An object which represents the response data of an HTTP transaction.
        /// </summary>
        IKayakServerResponse Response { get; }

        /// <summary>
        /// A bucket of user-defined state related to an HTTP transaction.
        /// </summary>
        IDictionary<object, object> Items { get; }
    }

    /// <summary>
    /// Represents the request portion of an HTTP transaction. 
    /// </summary>
    public interface IKayakServerRequest
    {
        /// <summary>
        /// Gets the HTTP verb component of the request's request line (e.g., GET, POST, DELETE, etc.).
        /// </summary>
        string Verb { get; }

        /// <summary>
        /// Gets the request URI component of the request's request line (e.g., /path/and?query=string).
        /// </summary>
        string RequestUri { get; }

        /// <summary>
        /// Gets the HTTP version component of the request's request line (e.g., HTTP/1.0).
        /// </summary>
        string HttpVersion { get; }

        /// <summary>
        /// Gets a dictionary representing the headers of the request.
        /// </summary>
        IDictionary<string, string> Headers { get; }

        /// <summary>
        /// Gets a stream representing the body portion of the request. The stream's length is limited
        /// to the value of the Content-Length header, if any. If no Content-Length header is
        /// present, the stream's length is unknown.
        /// </summary>
        RequestStream Body { get; }

        #region Derived properties

        /// <summary>
        /// The path component of the request's `RequestUri` property (e.g., /some/path).
        /// </summary>
        /// <remarks>
        /// Implementors may use the `GetPath()` extension method of `IKayakServerRequest` 
        /// to implement this property.
        /// </remarks>
        string Path { get; }

        /// <summary>
        /// A dictionary representation of the query string component of the request's
        /// `RequestUri` property.
        /// </summary>
        /// <remarks>
        /// Implementors may use the `GetQueryString()` extension method of `IKayakServerRequest`
        /// to implement this property.
        /// </remarks>
        IDictionary<string, string> QueryString { get; }

        #endregion
    }

    /// <summary>
    /// Represents the response portion of an HTTP transaction.
    /// </summary>
    public interface IKayakServerResponse
    {
        /// <summary>
        /// The status code component of the HTTP response status line (e.g., 200, 404, 301).
        /// </summary>
        int StatusCode { get; set; }

        /// <summary>
        /// The reason phrase component of the HTTP response status line (e.g., OK, Not Found, Found).
        /// </summary>
        string ReasonPhrase { get; set; }

        /// <summary>
        /// The HTTP version component of the HTTP response status line (e.g., HTTP/1.0).
        /// </summary>
        string HttpVersion { get; set; }

        /// <summary>
        /// A dictionary representing the headers of the HTTP response.
        /// </summary>
        IDictionary<string, string> Headers { get; set; }

        /// <summary>
        /// A stream representing the body portion of the HTTP response. The stream's length
        /// is limited to the value of the Content-Length header, if any. If no Content-Length
        /// header is present, the stream's length is unknown.
        /// </summary>
        ResponseStream Body { get; }
    }
}
