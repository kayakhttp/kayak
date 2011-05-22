using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using Kayak;
using Kayak.Http;

namespace KayakExamples
{
    class Channel : IHttpRequestDelegate
    {
        public void OnRequest(HttpRequestHead request, IDataProducer requestBody, 
            IHttpResponseDelegate response)
        {
            HttpResponseHead headers;
            IDataProducer body = null;

            if (request.Uri == "/")
            {
                headers = new HttpResponseHead()
                {
                    Status = "200 OK",
                    Headers = new Dictionary<string, string>() 
                    {
                        { "Content-Type", "text/plain" },
                        { "Content-Length", "20" },
                    }
                };
                body = new BufferedBody("Hello world.\r\nHello.");
            }
            else if (request.Uri == "/echo")
            {
                headers = new HttpResponseHead()
                {
                    Status = "200 OK",
                    Headers = new Dictionary<string, string>() 
                    {
                        { "Content-Type", "text/plain" },
                        { "Content-Length", request.Headers["Content-Length"] },
                        { "Connection", "close" }
                    }
                };
                body = requestBody;
            }
            else
            {
                var responseBody = "The resource you requested ('" + request.Uri + "') could not be found.";
                headers = new HttpResponseHead()
                {
                    Status = "404 Not Found",
                    Headers = new Dictionary<string, string>()
                    {
                        { "Content-Type", "text/plain" },
                        { "Content-Length", responseBody.Length.ToString() }
                    }
                };
                body = new BufferedBody(responseBody);
            }

            response.OnResponse(headers, body);
        }
    }

    class BufferedBody : IDataProducer
    {
        ArraySegment<byte> data;

        public BufferedBody(string data) : this(data, Encoding.UTF8) { }
        public BufferedBody(string data, Encoding encoding) : this(encoding.GetBytes(data)) { }
        public BufferedBody(byte[] data) : this(new ArraySegment<byte>(data)) { }
        public BufferedBody(ArraySegment<byte> data)
        {
            this.data = data;
        }

        public IDisposable Connect(IDataConsumer channel)
        {
            channel.OnData(data, null);
            channel.OnEnd();
            return null;
        }
    }
}
