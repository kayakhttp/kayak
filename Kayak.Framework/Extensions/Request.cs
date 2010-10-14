using System.Collections.Generic;
using System.IO;
using Kayak.Core;

namespace Kayak.Framework
{
    public static partial class Extensions
    {
        public static IHttpServerResponse ServeFile(this IHttpServerRequest request)
        {
            var info = request.Context.GetInvocationInfo();

            if (!(info.Result is FileInfo)) return null;

            var file = info.Result as FileInfo;

            var response = new BaseResponse();

            // TODO support 304, Range: header, etc.

            var headers = new Dictionary<string, string>();
            headers["Content-Length"] = file.Length.ToString();

            return new KayakResponse("200 OK", headers, file);
        }
    }
}
