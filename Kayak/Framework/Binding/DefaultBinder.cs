using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using LitJson;

namespace Kayak.Framework
{
    class DefaultBinder : IObservable<InvocationInfo>
    {
        MethodMap map;
        TypedJsonMapper mapper;
        IKayakContext context;

        public DefaultBinder(MethodMap map, TypedJsonMapper mapper, IKayakContext context)
        {
            this.map = map;
            this.mapper = mapper;
            this.context = context;
        }

        void BindArgumentsFromHeaders(InvocationInfo info, NameValueDictionary pathParams)
        {
            var request = context.Request;
            var parameters = info.Method.GetParameters();
            info.Arguments = new object[parameters.Length];

            foreach (ParameterInfo param in parameters)
            {
                // TODO also pull from cookies?
                string value = pathParams[param.Name] ?? request.QueryString[param.Name];

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

        IEnumerable<object> BindArgumentsFromBody(InvocationInfo info)
        {
            var parameters = info.Method.GetParameters().Where(p => RequestBodyAttribute.IsDefinedOn(p)).ToArray();

            if (parameters.Count() == 0 || context.Request.Body == null)
                yield break;

            MemoryStream buffer = new MemoryStream();
            yield return BufferRequestBody(buffer).AsCoroutine();

            var reader = new JsonReader(new StreamReader(buffer));

            if (parameters.Count() > 1)
                reader.Read(); // read array start

            foreach (var param in parameters)
                info.Arguments[param.Position] = mapper.Read(param.ParameterType, reader);

            if (parameters.Count() > 1)
                reader.Read(); // read array end
        }

        IEnumerable<object> BufferRequestBody(MemoryStream stream)
        {
            int bytesRead = 0;
            byte[] buffer = new byte[1024];

            while (true)
            {
                yield return context.Request.Body.ReadAsync(buffer, 0, buffer.Length).Do(n => bytesRead = n);

                if (bytesRead == 0)
                    break;

                stream.Write(buffer, 0, bytesRead);
            }

            buffer = null;
            stream.Position = 0;
        }

        public IEnumerable<object> Bind()
        {
            // wait for the headers to be read and the Request object to be populated
            yield return context.Take(1);

            InvocationInfo info = new InvocationInfo();
            bool invalidVerb = false;
            NameValueDictionary pathParams = null;

            // select a method
            info.Method = map.GetMethodForContext(context, out invalidVerb, out pathParams);

            if (info.Method == null)
                info.Method = typeof(DefaultResponses).GetMethod("NotFound");

            if (invalidVerb)
                info.Method = typeof(DefaultResponses).GetMethod("InvalidMethod");

            info.Target = Activator.CreateInstance(info.Method.DeclaringType);

            var service = info.Target as KayakService;

            if (service != null)
                service.Context = context;

            BindArgumentsFromHeaders(info, pathParams);
            yield return BindArgumentsFromBody(info).AsCoroutine();
            yield return info;
        }

        public IDisposable Subscribe(IObserver<InvocationInfo> observer)
        {
            return Bind().AsCoroutine().Where(o => o is InvocationInfo).Cast<InvocationInfo>().Subscribe(observer);
        }

        class DefaultResponses : KayakService
        {
            public void InvalidMethod()
            {
                Context.Response.StatusCode = 405;
                Context.Response.ReasonPhrase = "Invalid Method";
                //context.Response.Write("Invalid method: " + context.Request.Verb);
            }

            public void NotFound()
            {
                Context.Response.SetStatusToNotFound();
            }
        }
    }
}
