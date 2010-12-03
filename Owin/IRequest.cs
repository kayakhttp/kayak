using System;
using System.Collections.Generic;

namespace Owin
{
    /// <summary>
    /// The HTTP request provides the requested method, uri, headers, application items, and input body.
    /// </summary>
    public interface IRequest
    {
        /// <summary>
        /// Gets the request method.
        /// </summary>
        string Method { get; }

        /// <summary>
        /// Gets the requested uri.
        /// </summary>
        string Uri { get; }

        /// <summary>
        /// Gets the headers.
        /// </summary>
        /// <remarks>
        /// Each header key may have one or more values matching the HTTP spec.
        /// Example:
        ///   GET / HTTP/1.0
        ///   Accept: text/html;application/xml
        ///
        ///   Generates a headers dictionary with key "Accept" containing string "text/html;application/xml"
        /// </remarks>
        IDictionary<string, IEnumerable<string>> Headers { get; }
        
        /// <summary>Gets the application-specific items or settings.</summary>
        IDictionary<string, object> Items { get; }

        /// <summary>
        /// Begins the asynchronous read from the request body.
        /// </summary>
        /// <param name="buffer">The buffer.</param>
        /// <param name="offset">The offset.</param>
        /// <param name="count">The count.</param>
        /// <param name="callback">The callback.</param>
        /// <param name="state">The state.</param>
        /// <returns>An IAsyncResult that represents the asynchronous read, which could still be pending.</returns>
        /// <see href="http://msdn.microsoft.com/en-us/library/system.io.stream.beginread.aspx"/>
        IAsyncResult BeginReadBody(byte[] buffer, int offset, int count, AsyncCallback callback, object state);
        
        /// <summary>
        /// Ends the asynchronous read from the request body.
        /// </summary>
        /// <param name="result">The result.</param>
        /// <returns>The number of bytes returned from the stream.</returns>
        /// <see href="http://msdn.microsoft.com/en-us/library/system.io.stream.endread.aspx"/>
        int EndReadBody(IAsyncResult result);
    }
}
