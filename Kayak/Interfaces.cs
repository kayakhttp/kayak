using System;
using System.Collections.Generic;
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
        IObservable<int> WriteFile(string file);

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
        /// Returns an observable which, upon subscription, attempts to read an HTTP
        /// request from the socket. Reading stops after the end of the request headers
        /// is reached, and the observable completes. After that point, the
        /// status line properties and the `Headers` property should be populated.
        /// </summary>
        IObservable<Unit> Begin();

        /// <summary>
        /// Reads a chunk of request body data from the socket.
        /// </summary>
        IObservable<ArraySegment<byte>> Read();
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
        /// Writes a chunk of data to the response body, sending the headers first if necessary.
        /// After the headers are sent, the setters for the status line properties and the Headers 
        /// property will throw `InvalidOperationException` if invoked.
        /// </summary>
        IObservable<Unit> Write(byte[] buffer, int offset, int count);

        /// <summary>
        /// Writes a the named file to the response body, sending the headers first if necessary.
        /// After the headers are sent, the setters for the status line properties and the Headers 
        /// property will throw `InvalidOperationException` if invoked.
        /// </summary>
        IObservable<Unit> WriteFile(string file);

        /// <summary>
        /// Signals to the server implementation that processing of the HTTP transaction
        /// is finished. Writes the response headers to the socket if they haven't been
        /// written already. Implementations may close or reuse the socket.
        /// </summary>
        IObservable<Unit> End();
    }
}
