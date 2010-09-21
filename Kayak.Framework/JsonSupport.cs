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

        public static IObservable<IKayakContext> SerializeToJson(this IObservable<IKayakContext> contexts, TypedJsonMapper mapper)
        {
            return contexts.SelectMany(c => Observable.CreateWithDisposable<IKayakContext>(o =>
                c.SerializeResultToJson(mapper).Subscribe(u => { }, e => o.OnError(e), () => { o.OnNext(c); o.OnCompleted(); })
            ));
        }

        public static IObservable<Unit> SerializeResultToJson(this IKayakContext context, TypedJsonMapper mapper)
        {
            var info = context.GetInvocationInfo();

            if (info.Result != null)
                return context.WriteJsonResponse(info.Result, mapper);
            else if (info.Exception != null)
            {
                var exception = info.Exception;

                if (exception is TargetInvocationException)
                    exception = exception.InnerException;

                context.Response.SetStatusToInternalServerError();

                return context.WriteJsonResponse(new { Error = exception.Message }, mapper);
            }
            else return Observable.Empty<Unit>();
        }

        static IObservable<Unit> WriteJsonResponse(this IKayakContext context, object o, TypedJsonMapper mapper)
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

        public static IObservable<IKayakContext> DeserializeArgsFromJson(this IObservable<IKayakContext> contexts, TypedJsonMapper mapper)
        {
            return contexts.SelectMany(c => Observable.CreateWithDisposable<IKayakContext>(o =>
                c.DeserializeArgsFromJson(mapper).Subscribe(u => {}, e => o.OnError(e), () => { o.OnNext(c); o.OnCompleted(); })
            ));
        }

        public static IObservable<Unit> DeserializeArgsFromJson(this IKayakContext context, TypedJsonMapper mapper)
        {
            return context.DeserializeArgsFromJsonInternal(mapper).AsCoroutine<Unit>();
        }

        static IEnumerable<object> DeserializeArgsFromJsonInternal(this IKayakContext context, TypedJsonMapper mapper)
        {
            var info = context.GetInvocationInfo();

            var parameters = info.Method.GetParameters().Where(p => RequestBodyAttribute.IsDefinedOn(p));

            if (parameters.Count() != 0 && context.Request.Headers.GetContentLength() != 0)
            {
                IList<ArraySegment<byte>> requestBody = null;
                yield return BufferRequestBody(context).AsCoroutine<IList<ArraySegment<byte>>>().Do(d => requestBody = d);

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
