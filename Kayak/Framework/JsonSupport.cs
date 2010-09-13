using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using LitJson;

namespace Kayak.Framework
{
    public static partial class Extensions
    {
        public static IObservable<Unit> WriteJsonResponse(this object o, IKayakContext context, TypedJsonMapper mapper)
        {
            var buffer = new MemoryStream();
            var writer = new StreamWriter(buffer);

            bool minified = context.GetJsonOutputMinified();

            var response = context.Response;

            response.Headers["Content-Type"] = minified ? "application/json; charset=utf-8" : "text/plain; charset=utf-8";

            mapper.Write(o, new JsonWriter(writer) { Validate = false, PrettyPrint = !minified });
            writer.Flush();

            var bufferBytes = buffer.ToArray();

            context.Response.Headers["Content-Length"] = bufferBytes.Length.ToString();
            return context.Response.Body.WriteAsync(bufferBytes, 0, bufferBytes.Length);
        }
    }

    public class JsonHandler : IInvocationResultHandler
    {
        public TypedJsonMapper Mapper { get; set; }

        public JsonHandler(TypedJsonMapper mapper)
        {
            Mapper = mapper;
        }

        public IObservable<Unit> HandleResult(IKayakContext context, InvocationInfo info, object result)
        {
            return result.WriteJsonResponse(context, Mapper);
        }
    }

    public class JsonBinder : IInvocationArgumentBinder
    {
        public TypedJsonMapper Mapper { get; set; }

        public JsonBinder(TypedJsonMapper mapper)
        {
            Mapper = mapper;
        }

        public void BindArgumentsFromHeaders(IKayakContext context, InvocationInfo info)
        {
            return;
        }

        public IObservable<Unit> BindArgumentsFromBody(IKayakContext context, InvocationInfo info)
        {
            if (!RequestBodyAttribute.IsDefinedOnParameters(info.Method))
                return null;

            return BindArgumentsFromBodyInternal(context, info).AsCoroutine<Unit>();
        }

        IEnumerable<object> BindArgumentsFromBodyInternal(IKayakContext context, InvocationInfo info)
        {
            var parameters = info.Method.GetParameters().Where(p => RequestBodyAttribute.IsDefinedOn(p));

            if (parameters.Count() == 0 || context.Request.Body == null)
                yield break;

            IList<ArraySegment<byte>> requestBody = null;
            yield return BufferRequestBody(context).AsCoroutine<IList<ArraySegment<byte>>>().Do(d => requestBody = d);

            var reader = new JsonReader(new StringReader(requestBody.GetString()));

            if (parameters.Count() > 1)
                reader.Read(); // read array start

            foreach (var param in parameters)
                info.Arguments[param.Position] = Mapper.Read(param.ParameterType, reader);

            if (parameters.Count() > 1)
                reader.Read(); // read array end
        }

        IEnumerable<object> BufferRequestBody(IKayakContext context)
        {
            var bytesRead = 0;
            var contentLength = context.Request.Headers.GetContentLength();
            var result = new List<ArraySegment<byte>>();

            while (true)
            {
                ArraySegment<byte> data = default(ArraySegment<byte>);
                yield return context.Request.Body.ReadAsync().Do(d => data = d);

                result.Add(data);

                bytesRead += data.Count;

                if (bytesRead == contentLength)
                    break;
            }
        }
    }

    public static partial class Extensions
    {
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

        public static void AddJsonSupport(this KayakInvocationBehavior behavior)
        {
            var mapper = new TypedJsonMapper();
            mapper.AddDefaultOutputConversions();
            mapper.AddDefaultInputConversions();

            behavior.AddJsonSupport(mapper);
        }
        public static void AddJsonSupport(this KayakInvocationBehavior behavior, TypedJsonMapper mapper)
        {
            behavior.Binders.Add(new JsonBinder(mapper));
            behavior.ResultHandlers.Add(new JsonHandler(mapper));
        }
    }
}
