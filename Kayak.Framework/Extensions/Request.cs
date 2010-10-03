using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Kayak.Framework
{
    public static partial class Extensions
    {
        /// <summary>
        /// Returns a dictionary representation of the query string component from the `RequestUri`
        /// property of the `IKayakServerRequest`.
        /// </summary>
        public static IDictionary<string, string> GetQueryString(this IKayakServerRequest request)
        {
            return Kayak.Extensions.GetQueryString(request.RequestUri);
        }

        /// <summary>
        /// Returns the path component from the `RequestUri` property of the `IKayakServerRequest` (e.g., /some/path).
        /// </summary>
        public static string GetPath(this IKayakServerRequest request)
        {
            return Kayak.Extensions.GetPath(request.RequestUri);
        }
    }
}
