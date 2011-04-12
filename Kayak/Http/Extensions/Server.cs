using System;
using System.Diagnostics;

namespace Kayak.Http
{
    public static partial class Extensions
    {
        public static IHttpServer AsHttpServer(this IServer server)
        {
            return new HttpServer(server);
        }
    }
}
