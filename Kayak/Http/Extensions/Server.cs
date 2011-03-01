using System;
using System.Diagnostics;

namespace Kayak.Http
{
    public static partial class Extensions
    {
        public static void AcceptHttp(this IServer server, Func<IRequestDelegate> del)
        {
            server.Delegate = new HttpServerDelegate(del);
        }
    }
}
