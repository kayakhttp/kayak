using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Kayak.Core;
using LitJson;

namespace Kayak.Framework
{
    public static partial class Extensions
    {
        static readonly string MinifiedOutputKey = "JsonSerializerPrettyPrint";
        
        public static void SetJsonOutputMinified(this IDictionary<object, object> context, bool value)
        {
            context[MinifiedOutputKey] = value;
        }
        
        public static bool GetJsonOutputMinified(this IDictionary<object, object> context)
        {
            return
                context.ContainsKey(MinifiedOutputKey) &&
                (bool)Convert.ChangeType(context[MinifiedOutputKey], typeof(bool));
        }

        public static IHttpServerResponse GetJsonResponse(this InvocationInfo info, JsonMapper2 jsonMapper, bool minified)
        {
            var response = new BufferedResponse();

            response.Headers["Content-Type"] = minified ? "application/json; charset=utf-8" : "text/plain; charset=utf-8";

            if (info.Result != null)
            {
                response.Add(GetJsonRepresentation(info.Result, jsonMapper, minified));
                response.SetContentLength();
            }
            else if (info.Exception != null)
            {
                var exception = info.Exception;

                if (exception is TargetInvocationException)
                    exception = exception.InnerException;

                response.Status = "503 Internal Server Error";
                response.Add(GetJsonRepresentation(new { error = exception.Message }, jsonMapper, minified));
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

        public static IObservable<Unit> DeserializeArgsFromJson(this InvocationInfo info, IHttpServerRequest request, JsonMapper2 mapper)
        {
            Func<int, IObservable<LinkedList<ArraySegment<byte>>>> bufferRequestBody =
                i => BufferRequestBody(request).AsCoroutine<LinkedList<ArraySegment<byte>>>();
            return info.DeserializeArgsFromJsonInternal(mapper, bufferRequestBody, request.Headers.GetContentLength()).AsCoroutine<Unit>();
        }

        internal static IEnumerable<object> DeserializeArgsFromJsonInternal(this InvocationInfo info, 
            JsonMapper2 mapper, Func<int, IObservable<LinkedList<ArraySegment<byte>>>> bufferRequestBody, int contentLength)
        {
            var parameters = info.Method.GetParameters().Where(p => RequestBodyAttribute.IsDefinedOn(p));

            if (parameters.Count() != 0 && contentLength != 0)
            {
                LinkedList<ArraySegment<byte>> requestBody = null;
                yield return bufferRequestBody(contentLength).Do(d => requestBody = d);

                var reader = new JsonReader(new StringReader(requestBody.GetString()));

                if (parameters.Count() > 1)
                    reader.Read(); // read array start

                foreach (var param in parameters)
                    info.Arguments[param.Position] = mapper.Read(param.ParameterType, reader);

                //if (parameters.Count() > 1)
                //    reader.Read(); // read array end
            }
        }

        // wish i had an evented JSON parser.
        static IEnumerable<object> BufferRequestBody(Func<IObservable<ArraySegment<byte>>> read, int contentLength)
        {
            var bytesRead = 0;
            var result = new LinkedList<ArraySegment<byte>>();

            while (true)
            {
                ArraySegment<byte> data = default(ArraySegment<byte>);
                yield return read().Do(d => data = d);

                result.AddLast(data);

                bytesRead += data.Count;

                if (bytesRead == contentLength)
                    break;
            }

            yield return result;
        }

        static IEnumerable<object> BufferRequestBody(IHttpServerRequest request)
        {
            var result = new LinkedList<ArraySegment<byte>>();
            var buffer = new byte[2048];
            var totalBytesRead = 0;
            var bytesRead = 0;

            var body = request.GetBody();

            foreach (var getChunk in body)
                yield return getChunk(new ArraySegment<byte>(buffer, 0, buffer.Length)).Do(n => bytesRead = n);

            result.AddLast(new ArraySegment<byte>(buffer, 0, bytesRead));
            totalBytesRead += bytesRead;
            yield return result;
        }
    }
}
