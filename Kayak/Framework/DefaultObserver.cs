using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using System.Disposables;
using LitJson;
using System.IO;

namespace Kayak.Framework
{
    class DefaultObserver : IObserver<object>
    {
        IKayakInvocation invocation;
        TypedJsonMapper mapper;
        object result;

        public DefaultObserver(TypedJsonMapper mapper, IKayakInvocation invocation)
        {
            this.invocation = invocation;
            this.mapper = mapper;
        }

        // Called after an invocation is made.
        public void OnCompleted()
        {
            // serialize as JSON or something
            if (invocation.Info.Method.ReturnType != typeof(void))
            {
                var buffer = new MemoryStream();
                var writer = new StreamWriter(buffer);
                
                bool minified = invocation.Context.GetJsonOutputMinified();

                var response = invocation.Context.Response;

                response.Headers["Content-Type"] = minified ? "application/json; charset=utf-8" : "text/plain; charset=utf-8";

                mapper.Write(result, new JsonWriter(writer) { Validate = false, PrettyPrint = !minified });
                writer.Flush();

                var bufferBytes = buffer.ToArray();

                invocation.Context.Response.Headers["Content-Length"] = bufferBytes.Length.ToString();
                invocation.Context.Response.Body.WriteAsync(bufferBytes, 0, bufferBytes.Length)
                    .Subscribe(u => { }, () => invocation.Context.OnCompleted());
            }
            else
            {
                // log message?
                invocation.Context.OnCompleted();
            }
        }

        public void OnError(Exception exception)
        {
            invocation.Context.Response.SetStatusToInternalServerError();
            invocation.Context.OnCompleted();
        }

        // called with the result of the invocation.
        public void OnNext(object value)
        {
            result = value;
        }
    }
}
