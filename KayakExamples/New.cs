using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using Kayak;
using Kayak.Http;

namespace KayakExamples
{
    class HttpChannel : IHttpChannel
    {
        public void OnRequest(HttpRequestHead request, IDataProducer requestBody, 
            IHttpResponseDelegate response)
        {
            Debug.WriteLine("OnRequest");

            // hmmm...
            if (request.Uri == "/")
            {
                response.OnResponse(new HttpResponseHead()
                {
                    Status = "200 OK",
                    Headers = new Dictionary<string, string>() 
                    {
                        { "Content-Type", "text/plain" },
                        { "Content-Length", "20" },
                    }
                },
                new StringBody("Hello world.\r\nHello."));

                Debug.WriteLine("OnRequest (hello): Ended response.");
            }
            else if (request.Uri == "/echo")
            {
                response.OnResponse(new HttpResponseHead()
                {
                    Status = "200 OK",
                    Headers = new Dictionary<string, string>() 
                    {
                        { "Content-Type", "text/plain" },
                        { "Connection", "close" }
                    }
                }, requestBody);
            }
        }
    }

    class StringBody : IDataProducer
    {
        string body;
        public StringBody(string body)
        {
            this.body = body;
        }

        public IDisposable Connect(IDataConsumer channel)
        {
            channel.OnData(new ArraySegment<byte>(Encoding.ASCII.GetBytes(body)), null);
            channel.OnEnd();

            return null;
        }
    }
}
