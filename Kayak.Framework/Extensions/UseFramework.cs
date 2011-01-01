using System;
using System.Collections.Generic;
using System.Reflection;
using LitJson;
using System.Threading.Tasks;

namespace Kayak.Framework
{
    public static partial class Extensions
    {
        public static KayakFramework CreateFramework(IEnumerable<Type> types)
        {
            var mm = types.CreateMethodMap();
            var jm = new JsonMapper2();
            jm.AddDefaultInputConversions();
            jm.AddDefaultOutputConversions();

            return new KayakFramework(mm, jm);
        }

        public static IDisposable UseFramework(this IObservable<ISocket> server)
        {
            return server.UseFramework(Assembly.GetCallingAssembly().GetTypes());
        }

        public static IDisposable UseFramework(this IObservable<ISocket> server, IEnumerable<Type> types)
        {
            return server.Host(new ErrorHandlingMiddleware(CreateFramework(types), new ErrorHandler()), TaskScheduler.Default);
        }
    }
}
