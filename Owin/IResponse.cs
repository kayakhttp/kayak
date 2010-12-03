using System;
using System.Collections;
using System.Collections.Generic;

namespace Owin
{
    /// <summary>
    /// The HTTP response provides the status, headers, and body from an <see cref="IApplication"/>.
    /// </summary>
    public interface IResponse
    {
        /// <summary>
        /// Gets the status code and description.
        /// </summary>
        /// <remarks>The string should follow the format of "200 OK".</remarks>
        string Status { get; }

        /// <summary>
        /// Gets the headers.
        /// </summary>
        /// <remarks>
        /// Each header key may have one or more values matching the HTTP spec.
        /// Example:
        ///   HTTP/1.1 200 OK
        ///   Set-Cookie: foo=bar
        ///   Set-Cookie: baz=quux
        /// 
        ///   Generates a headers dictionary with key "Set-Cookie" containing string ["foo=bar";"baz=quux"]
        /// </remarks>
        IDictionary<string, IEnumerable<string>> Headers { get; }

        /// <summary>
        /// Gets the body <see cref="IEnumerable"/>.
        /// </summary>
        /// <returns>The response as an <see cref="IEnumerable"/>.</returns>
        /// <remarks>
        /// The <see cref="IEnumerable"/> is not guaranteed to be hot.
        /// This method should be considered safe to generate either a cold or hot enumerable
        /// so that it _could_ be called more than once, though the expectation is only one call.
        /// </remarks>
        IEnumerable<object> GetBody();
    }
}
