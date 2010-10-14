using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using Kayak.Core;

namespace Kayak.Framework
{
    public static partial class Extensions
    {
        public static IHttpServerResponse ServeFile(this InvocationInfo info)
        {
            if (!(info.Result is FileInfo)) return null;

            var file = info.Result as FileInfo;

            var response = new BaseResponse();

            response.Headers["Content-Length"] = file.Length.ToString();
            response.SetBody(new object[] { file });
            return response;
        }
    }
}
