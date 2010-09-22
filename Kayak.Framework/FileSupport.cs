using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace Kayak.Framework
{
    public static partial class Extensions
    {
        public static IObservable<Unit> ServeFile(this IKayakContext context)
        {
            var info = context.GetInvocationInfo();

            if (!(info.Result is FileInfo)) return null;

            var file = info.Result as FileInfo;
            context.Response.Headers["Content-Length"] = file.Length.ToString();
            return context.Response.WriteFile(file.Name);
        }
    }
}
