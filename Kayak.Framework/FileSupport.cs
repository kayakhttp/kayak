using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace Kayak.Framework
{
    class FileHandler : IResultHandler
    {
        public IObservable<Unit> HandleResult(IKayakContext context, InvocationInfo info, object result)
        {
            var file = result as FileInfo;

            if (file == null) return null;

            context.Response.Headers["Content-Length"] = file.Length.ToString();

            // this is a pretty janky way to cast an observable...
            return WriteFile(context, file).AsCoroutine<Unit>();
        }

        IEnumerable<object> WriteFile(IKayakContext context, FileInfo file)
        {
            yield return context.Response.WriteFile(file.Name);
        }
    }

    //public static partial class Extensions
    //{
    //    public static void AddFileSupport(this KayakInvocationBehavior behavior)
    //    {
    //        behavior.ResultHandlers.Add(new FileHandler());
    //    }
    //}
}
