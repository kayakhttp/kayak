using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using LitJson;
using System.Text.RegularExpressions;

namespace Kayak.Framework
{
    public static partial class Extensions
    {
        public static IDisposable UseFramework(this IObservable<ISocket> connections, Type[] types)
        {
            var map = types.CreateMethodMap();

            var mapper = new TypedJsonMapper();
            mapper.AddDefaultOutputConversions();
            mapper.AddDefaultInputConversions();

            return connections
                .Select(connection => new KayakContext(connection))
                .Select(context => new KayakInvocation(context, new DefaultBinder(map, mapper, context)))
                .Subscribe(invocation => invocation.Subscribe(new DefaultObserver(mapper, invocation)));
        }

        public static IDisposable UseFramework(this IObservable<ISocket> connections)
        {
            return connections.UseFramework(Assembly.GetCallingAssembly().GetTypes());
        }

        static readonly string MinifiedOutputKey = "JsonSerializerPrettyPrint";

        public static void SetJsonOutputMinified(this IKayakContext context, bool value)
        {
            context.Items[MinifiedOutputKey] = value;
        }

        public static bool GetJsonOutputMinified(this IKayakContext context)
        {
            return
                context.Items.ContainsKey(MinifiedOutputKey) &&
                (bool)Convert.ChangeType(context.Items[MinifiedOutputKey], typeof(bool));
        }

        static Regex ampmRegex = new Regex(@"([ap])m?", RegexOptions.IgnoreCase); // am,pm,a,p
        static Regex colonRegex = new Regex(@"(.* )([0-9]{1,2})([^:]*$)"); // last 2-digit number with no following colons
        static List<string> trueValues = new List<string>(new string[] { "true", "t", "yes", "y", "on", "1" });

        // not too sure about this being public
        public static T Coerce<T>(this string s)
        {
            return (T)Coerce(s, typeof(T));
        }

        // not too sure about this being public
        public static object Coerce(this string s, Type t)
        {
            if (s == null && t.IsValueType)
            {
                // turn unspecified params into default primitive values
                return Activator.CreateInstance(t);
            }
            else if (t == typeof(bool))
            {
                s = s.ToLower();
                return trueValues.Contains(s);
            }
            else if (t == typeof(DateTime))
            {
                return ParseDateTime(s);
            }
            else
            {
                // let the system do it
                try
                {
                    return Convert.ChangeType(s, t);
                }
                catch (Exception e)
                {
                    throw new ArgumentException("Could not convert '" + s + "' to " + t + ".", e);
                }
            }
        }

        /// <summary>
        /// Equivalent to DateTime.Parse(), except it accomodates am/pm strings better.
        /// </summary>
        private static DateTime ParseDateTime(string date)
        {
            if (string.IsNullOrEmpty(date))
                throw new Exception("Attemped to parse an empty date!");

            // mono can't handle am/pm without some whitespace.
            date = ampmRegex.Replace(date, " $1m"); // turn "a" or "am" into " am"

            // mono also can't handle times without the colon!  lame!
            date = colonRegex.Replace(date, "$1$2:00$3");

            DateTime dt;
            if (DateTime.TryParse(date, out dt)) return dt;

            string message = "Could not parse the DateTime string '{0}'.";
            throw new FormatException(string.Format(message, date));
        }
    }
}
