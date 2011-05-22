using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Text;
using Kayak;
using Kayak.Http;

namespace KayakExamples
{
    // demonstrates how to use kayak.
    //
    // important bits: kayak uses a single worker thread, represented
    // by KayakScheduler. You can post work to the scheduler from any
    // thread by using its Post() method.
    //
    // if an exception bubbles up to the scheduler, it's passed to the 
    // scheduler delegate.
    //
    // HTTP requests are handled by an IHttpRequestDelegate. the OnRequest
    // method receives the request headers and body as well as an
    // IHttpResponseDelegate which can be used to generate a response.
    //
    // Request and response body streams are represented by IDataProducer.
    // the semantics of IDataProducer are nearly identical to those of
    // IObservable, the difference being the OnData method of IDataConsumer 
    // (analogous to the OnNext method of IObserver) takes an additional 
    // continuation argument and returns a bool. this is a mechanism to
    // enable a consumer to 'throttle back' a producer.
    //
    // a consumer should return true if it will invoke the continuation, and
    // false otherwise. if the consumer returns true, the producer should not 
    // call OnNext again until the continuation it provided to the consumer is
    // invoked. a producer may provide a null continuation to prohibit the
    // consumer from 'throttling back' the producer.
    class Program
    {
        static void Main(string[] args)
        {
#if DEBUG
            Debug.Listeners.Add(new TextWriterTraceListener(Console.Out));
            Debug.AutoFlush = true;
#endif

            var scheduler = new KayakScheduler(new SchedulerDelegate());
            scheduler.Post(() =>
            {
                KayakServer.Factory
                    .CreateHttp(new RequestDelegate())
                    .Listen(new IPEndPoint(IPAddress.Any, 8080));
            });

            // runs scheduler on calling thread. this method will block until
            // someone calls Stop() on the scheduler.
            scheduler.Start();
        }

        class SchedulerDelegate : ISchedulerDelegate
        {
            public void OnException(IScheduler scheduler, Exception e)
            {
                Debug.WriteLine("Error on scheduler.");
                e.DebugStacktrace();
            }

            public void OnStop(IScheduler scheduler)
            {

            }
        }

        class RequestDelegate : IHttpRequestDelegate
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
                // null continuation, consumer must swallow the data immediately.
                channel.OnData(data, null);
                channel.OnEnd();
                return null;
            }
        }
    }
}
