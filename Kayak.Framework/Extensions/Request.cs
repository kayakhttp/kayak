using System.Collections.Generic;
using System.IO;
using Owin;

namespace Kayak.Framework
{
    public static partial class Extensions
    {
        public static IResponse ServeFile(this IRequest request)
        {
            var info = request.Items.GetInvocationInfo();

            if (!(info.Result is FileInfo)) return null;

            var file = info.Result as FileInfo;

            // TODO support 304, Range: header, etc.

            var headers = new Dictionary<string, IEnumerable<string>>();
            headers["Content-Length"] = new string[] { file.Length.ToString() };

            return new KayakResponse("200 OK", headers, file);
        }
    }
}
