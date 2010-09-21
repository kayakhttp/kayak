using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace Kayak.Framework
{
    public static partial class Extensions
    {
        public static IObservable<IKayakContext> ServeFile(this IObservable<IKayakContext> contexts)
        {
            return contexts.SelectMany(c => Observable.CreateWithDisposable<IKayakContext>(o =>
                    c.ServeFile().Subscribe(u => { }, e => o.OnError(e), () => { o.OnNext(c); o.OnCompleted(); })
                ));
        }

        public static IObservable<Unit> ServeFile(this IKayakContext context)
        {
            var info = context.GetInvocationInfo();

            if (info.Result is FileInfo)
            {
                var file = info.Result as FileInfo;
                context.Response.Headers["Content-Length"] = file.Length.ToString();
                return context.Response.WriteFile(file.Name);
            }
            else return Observable.Empty<Unit>();
        }
    }
}
