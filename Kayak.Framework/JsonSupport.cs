using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using LitJson;
using System.Reflection;

namespace Kayak.Framework
{
    public static partial class Extensions
    {
        public static IObservable<Unit> WriteJsonResponse(this IKayakContext context, object o, TypedJsonMapper mapper)
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
            return context.Response.Write(bufferBytes, 0, bufferBytes.Length);
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

        public static IObservable<object> SerializeToJson(this IObservable<object> invocation, IKayakContext c, TypedJsonMapper mapper)
        {
            return invocation.SerializeToJsonInternal(c, mapper).AsCoroutine<object>();
        }

        static IEnumerable<object> SerializeToJsonInternal(this IObservable<object> invocation, IKayakContext c, TypedJsonMapper mapper)
        {
            object result = null;
            Exception exception = null;

            yield return invocation.Do(o => result = o, e => exception = e, () => { }).Catch(Observable.Empty<object>());

            if (result != null)
                yield return c.WriteJsonResponse(result, mapper);
            else if (exception != null)
            {
                if (exception is TargetInvocationException)
                    exception = exception.InnerException;

                c.Response.SetStatusToInternalServerError();

                yield return c.WriteJsonResponse(new { Error = exception.Message }, mapper);
            }
        }

        public static IObservable<InvocationInfo> BindJsonArgs(this IObservable<InvocationInfo> bind, 
            IKayakContext context, TypedJsonMapper mapper)
        {
            return BindJsonArgsInternal(bind, context, mapper).AsCoroutine<InvocationInfo>();
        }

        static IEnumerable<object> BindJsonArgsInternal(IObservable<InvocationInfo> bind, 
            IKayakContext context, TypedJsonMapper mapper)
        {
            InvocationInfo info = null;

            yield return bind.Do(i => info = i);

            var parameters = info.Method.GetParameters().Where(p => RequestBodyAttribute.IsDefinedOn(p));

            if (parameters.Count() == 0 || context.Request.Headers.GetContentLength() == 0)
            {
                yield return info;
                yield break;
            }

            IList<ArraySegment<byte>> requestBody = null;
            yield return BufferRequestBody(context).AsCoroutine<IList<ArraySegment<byte>>>().Do(d => requestBody = d);

            var reader = new JsonReader(new StringReader(requestBody.GetString()));

            if (parameters.Count() > 1)
                reader.Read(); // read array start

            foreach (var param in parameters)
                info.Arguments[param.Position] = mapper.Read(param.ParameterType, reader);

            if (parameters.Count() > 1)
                reader.Read(); // read array end
        }

        static IEnumerable<object> BufferRequestBody(IKayakContext context)
        {
            var bytesRead = 0;
            var contentLength = context.Request.Headers.GetContentLength();
            var result = new List<ArraySegment<byte>>();

            while (true)
            {
                ArraySegment<byte> data = default(ArraySegment<byte>);
                yield return context.Request.Read().Do(d => data = d);

                result.Add(data);

                bytesRead += data.Count;

                if (bytesRead == contentLength)
                    break;
            }
        }
    }
}
