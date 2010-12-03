using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Owin;

namespace Kayak.Framework
{
    public class MethodMap
    {
        public static readonly string PathParamsContextKey = "PathParams";

        class MethodMatch
        {
            public MethodInfo Method;
            public int Score;
            public Dictionary<string, string> Params = new Dictionary<string, string>();
        }

        string parameter;
        Dictionary<string, MethodMap> children = new Dictionary<string, MethodMap>();
        Dictionary<string, MethodInfo> methods = new Dictionary<string, MethodInfo>();

        public MethodMap() : this(null) { }

        MethodMap(string name)
        {
            if (name != null && name.StartsWith("{") && name.EndsWith("}"))
                parameter = name.TrimStart('{').TrimEnd('}');
        }

        public void MapMethod(string path, string verb, MethodInfo method)
        {
            MapMethod(path.TrimEnd('/').Split('/'), verb, method);
        }

        void MapMethod(IEnumerable<string> pathComponents, string verb, MethodInfo method)
        {
            string component = pathComponents.FirstOrDefault();

            if (component == null)
                methods[verb] = method;
            else
            {
                if (!children.ContainsKey(component))
                    children[component] = new MethodMap(component);

                children[component].MapMethod(pathComponents.Skip(1), verb, method);
            }
        }

        //public MethodInfo GetMethodForContext(IKayakContext context)
        //{
        //    var request = context.Request;

        //    bool notFound, invalidMethod;
        //    var method = GetMethod(context.Request.GetPath(), context.Request.Verb, context.Items, out notFound, out invalidMethod);

        //    if (notFound)
        //        return typeof(DefaultResponses).GetMethod("NotFound");

        //    if (invalidMethod)
        //        return typeof(DefaultResponses).GetMethod("InvalidMethod");

        //    return method;
        //}

        public MethodInfo GetMethod(string path, string verb, IDictionary<string, object> context, out bool notFound, out bool invalidMethod)
        {
            notFound = false;
            invalidMethod = false;

            MethodMatch match = GetBestMatch(path.TrimEnd('/').Split('/'), verb);

            if (match == null)
            {
                notFound = true;
                return null;
            }

            if (match.Method == null)
            {
                invalidMethod = true;
                return null;
            }

            context[PathParamsContextKey] = match.Params;

            return match.Method;
        }

        MethodMatch GetBestMatch(IEnumerable<string> components, string verb)
        {
            string name = components.FirstOrDefault();

            if (name == null)
            {
                MethodMatch match = new MethodMatch();

                if (methods.ContainsKey(verb))
                {
                    match.Method = methods[verb];
                }

                return match;
            }

            IEnumerable<string> remaining = components.Skip(1);

            // prefer direct matches
            if (children.ContainsKey(name))
            {
                var match = children[name].GetBestMatch(remaining, verb);

                if (match != null)
                    match.Score++;

                return match;
            }

            // pick the best dynamic child
            var matches = from entry in children
                          where entry.Value.parameter != null
                          select new { Param = entry.Value.parameter, Match = entry.Value.GetBestMatch(remaining, verb) };

            var bestMatch = matches.Where(m => m.Match != null).OrderByDescending(m => m.Match.Score).FirstOrDefault();

            if (bestMatch != null)
            {
                bestMatch.Match.Params.Add(bestMatch.Param, name);
                return bestMatch.Match;
            }
            else return null;
        }
    }

    class DefaultResponses
    {
        public static IResponse InvalidMethodResponse(string verb)
        {
            var response = new BufferedResponse();
            response.Status = "405 Invalid Method";
            response.Headers["Content-Type"] = new string[] { "text/html" };
            response.Add("<h1>Invalid method</h1><p>The requested resource does not support the method '" + verb + "'.");
            return response;
        }

        public static IResponse NotFoundResponse()
        {
            var response = new BufferedResponse();
            response.Status = "404 Not Found";
            response.Headers["Content-Type"] = new string[] { "text/html" };
            response.Add("<h1>Not Found</h1><p>The requested resource was not found.");
            return response;
        }

    }

    public static class MethodMapExtensions
    {
        public static MethodMap CreateMethodMap(this IEnumerable<Type> types)
        {
            var map = new MethodMap();

            foreach (var method in types.SelectMany(t => t.GetMethods()))
            {
                var paths = PathAttribute.PathsForMethod(method);

                if (paths.Length == 0) continue;

                var verbs = VerbAttribute.VerbsForMethod(method);

                if (verbs.Length == 0)
                    verbs = new string[] { "GET" };

                foreach (var path in paths)
                    foreach (var verb in verbs)
                        map.MapMethod(path, verb, method);
            }

            return map;
        }

        public static IDictionary<string, string> GetPathParameters(this IDictionary<string, object> context)
        {
            return context.ContainsKey(MethodMap.PathParamsContextKey) ?
                context[MethodMap.PathParamsContextKey] as IDictionary<string, string> : null;
        }
    }
}
