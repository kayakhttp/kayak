using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Kayak.Framework
{
    class MethodMap
    {
        public static readonly string PathParamsContextKey = "PathParams";

        class MethodMatch
        {
            public MethodInfo Method;
            public int Score;
            public NameValueDictionary Params = new NameValueDictionary();
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

        public MethodInfo GetMethodForContext(IKayakContext context, out bool invalidVerb, out NameValueDictionary pathParams)
        {
            var request = context.Request;
            invalidVerb = false;
            pathParams = null;

            MethodMatch match = GetBestMatch(request.Path.TrimEnd('/').Split('/'), request.Verb);

            if (match == null) return null;

            if (match.Method == null)
                invalidVerb = true;

            pathParams = match.Params;

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
}
