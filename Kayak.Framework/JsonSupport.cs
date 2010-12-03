using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Owin;
using LitJson;

namespace Kayak.Framework
{
    public static partial class Extensions
    {
        static readonly string MinifiedOutputKey = "JsonSerializerPrettyPrint";
        
        public static void SetJsonOutputMinified(this IDictionary<string, object> context, bool value)
        {
            context[MinifiedOutputKey] = value;
        }
        
        public static bool GetJsonOutputMinified(this IDictionary<string, object> context)
        {
            return
                context.ContainsKey(MinifiedOutputKey) &&
                (bool)Convert.ChangeType(context[MinifiedOutputKey], typeof(bool));
        }

        public static IResponse GetJsonResponse(this InvocationInfo info, JsonMapper2 jsonMapper, bool minified)
        {
            var response = new BufferedResponse();

            response.Headers["Content-Type"] = new string[] { minified ? "application/json; charset=utf-8" : "text/plain; charset=utf-8" };

            if (info.Exception != null)
            {
                var exception = info.Exception;

                if (exception is TargetInvocationException)
                    exception = exception.InnerException;

                response.Status = "503 Internal Server Error";
                response.Add(GetJsonRepresentation(new { error = exception.Message }, jsonMapper, minified));
                response.SetContentLength();
            } 
            else
            {
                response.Add(GetJsonRepresentation(info.Result, jsonMapper, minified));
                response.SetContentLength();
            }

            return response;
        }

        static byte[] GetJsonRepresentation(object o, JsonMapper2 mapper, bool minified)
        {
            var buffer = new MemoryStream();
            var writer = new StreamWriter(buffer);

            mapper.Write(o, new JsonWriter(writer) { Validate = false, PrettyPrint = !minified });
            writer.Flush();

            return buffer.ToArray();
        }

        public static IObservable<Unit> DeserializeArgsFromJson(this InvocationInfo info, IRequest request, JsonMapper2 mapper)
        {
            return info.DeserializeArgsFromJsonInternal(mapper, request).AsCoroutine<Unit>();
        }

        internal static IEnumerable<object> DeserializeArgsFromJsonInternal(this InvocationInfo info, 
            JsonMapper2 mapper, IRequest request)
        {
            var contentLength = request.Headers.GetContentLength();
            var parameters = info.Method.GetParameters().Where(p => RequestBodyAttribute.IsDefinedOn(p));

            if (parameters.Count() != 0 && contentLength != 0)
            {
                IEnumerable<ArraySegment<byte>> requestBody = null;
                yield return request.BufferRequestBody().Do(d => requestBody = d);

                var reader = new JsonReader(new StringReader(requestBody.GetString()));

                if (parameters.Count() > 1)
                    reader.Read(); // read array start

                foreach (var param in parameters)
                    info.Arguments[param.Position] = mapper.Read(param.ParameterType, reader);

                //if (parameters.Count() > 1)
                //    reader.Read(); // read array end
            }
        }
    }
}
