using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Kayak.Framework
{
    public static partial class Extensions
    {
        static string CoerceContextKey = "CoerceFunction";

        public static object Coerce(this IDictionary<string, object> context, string str, Type type)
        {
            var coerce = (Func<string, Type, object>)
                (context.ContainsKey(CoerceContextKey) ? context[CoerceContextKey] : null);

            if (coerce == null)
                SetCoerce(context, coerce = Coerce);

            return coerce(str, type);
        }

        public static void SetCoerce(this IDictionary<string, object> context, Func<string, Type, object> coerce)
        {
            context[CoerceContextKey] = coerce;
        }

        static Regex ampmRegex = new Regex(@"([ap])m?", RegexOptions.IgnoreCase); // am,pm,a,p
        static Regex colonRegex = new Regex(@"(.* )([0-9]{1,2})([^:]*$)"); // last 2-digit number with no following colons
        static List<string> trueValues = new List<string>(new string[] { "true", "t", "yes", "y", "on", "1" });

        static object Coerce(string s, Type t)
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
                DateTime dt;
                if (DateTime.TryParse(s, out dt)) return dt;

                string message = "Could coerce to DateTime: '{0}'.";
                throw new FormatException(string.Format(message, s));
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
    }
}
