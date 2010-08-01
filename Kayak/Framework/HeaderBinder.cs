using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using System.Text.RegularExpressions;

namespace Kayak.Framework
{
    public class HeaderBinder : IInvocationArgumentBinder
    {
        public void BindArgumentsFromHeaders(IKayakContext context, InvocationInfo info)
        {
            var request = context.Request;
            var parameters = info.Method.GetParameters().Where(p => !RequestBodyAttribute.IsDefinedOn(p));
            var pathParams = context.Items[MethodMap.PathParamsContextKey] as NameValueDictionary;

            foreach (ParameterInfo param in parameters)
            {
                string value = null;

                if (pathParams != null)
                    value = pathParams[param.Name];

                if (value == null)
                    value = request.QueryString[param.Name];

                // TODO also pull from cookies?

                if (value != null)
                    try
                    {
                        info.Arguments[param.Position] = value.Coerce(param.ParameterType);
                    }
                    catch (Exception e)
                    {
                        throw new Exception(string.Format(
                            "Could not convert '{0}' to the type required by {1} ({2}).",
                            value, param.Name, param.ParameterType), e);
                    }
            }
        }

        public IObservable<Unit> BindArgumentsFromBody(IKayakContext context, InvocationInfo info)
        {
            return null;
        }
    }

    public static partial class Extensions
    {
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
