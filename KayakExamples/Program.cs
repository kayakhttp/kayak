using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
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

            var scheduler = KayakScheduler.Factory.Create(new SchedulerDelegate());
            scheduler.Post(() =>
            {
                KayakServer.Factory
                    .CreateHttp(new RequestDelegate(), scheduler)
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
                e.DebugStackTrace();
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

                if (request.Uri.StartsWith("/"))
                {
                    var body = string.Format(
                        "Hello world.\r\nHello.\r\n\r\nUri: {0}\r\nPath: {1}\r\nQuery:{2}\r\nFragment: {3}\r\n",
                        request.Uri,
                        request.Path,
                        request.QueryString,
                        request.Fragment);

                    var headers = new HttpResponseHead()
                    {
                        Status = "200 OK",
                        Headers = new Dictionary<string, string>() 
                    {
                        { "Content-Type", "text/plain" },
                        { "Content-Length", body.Length.ToString() },
                    }
                    };
                    response.OnResponse(headers, new BufferedProducer(body));
                }
                else if (request.Uri.StartsWith("/bufferedecho"))
                {
                    // when you subecribe to the request body before calling OnResponse,
                    // the server will automatically send 100-continue if the client is 
                    // expecting it.
                    requestBody.Connect(new BufferedConsumer(bufferedBody =>
                    {
                        var headers = new HttpResponseHead()
                        {
                            Status = "200 OK",
                            Headers = new Dictionary<string, string>() 
                                {
                                    { "Content-Type", "text/plain" },
                                    { "Content-Length", request.Headers["Content-Length"] },
                                    { "Connection", "close" }
                                }
                        };
                        response.OnResponse(headers, new BufferedProducer(bufferedBody));
                    }, error =>
                    {
                        // XXX
                        // uh oh, what happens?
                    }));
                }
                else if (request.Uri.StartsWith("/echo"))
                {
                    var headers = new HttpResponseHead()
                    {
                        Status = "200 OK",
                        Headers = new Dictionary<string, string>() 
                        {
                            { "Content-Type", "text/plain" },
                            { "Content-Length", request.Headers["Content-Length"] },
                            { "Connection", "close" }
                        }
                    };

                    // if you call OnResponse before subscribing to the request body,
                    // 100-continue will not be sent before the response is sent.
                    // per rfc2616 this response must have a 'final' status code,
                    // but the server does not enforce it.
                    response.OnResponse(headers, requestBody);
                }
                else
                {
                    var responseBody = "The resource you requested ('" + request.Uri + "') could not be found.";
                    var headers = new HttpResponseHead()
                    {
                        Status = "404 Not Found",
                        Headers = new Dictionary<string, string>()
                    {
                        { "Content-Type", "text/plain" },
                        { "Content-Length", responseBody.Length.ToString() }
                    }
                    };
                    var body = new BufferedProducer(responseBody);

                    response.OnResponse(headers, body);
                }
            }
        }

        class BufferedProducer : IDataProducer
        {
            ArraySegment<byte> data;

            public BufferedProducer(string data) : this(data, Encoding.UTF8) { }
            public BufferedProducer(string data, Encoding encoding) : this(encoding.GetBytes(data)) { }
            public BufferedProducer(byte[] data) : this(new ArraySegment<byte>(data)) { }
            public BufferedProducer(ArraySegment<byte> data)
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

        class BufferedConsumer : IDataConsumer
        {
            List<ArraySegment<byte>> buffer = new List<ArraySegment<byte>>();
            Action<string> resultCallback;
            Action<Exception> errorCallback;

            public BufferedConsumer(Action<string> resultCallback,
        Action<Exception> errorCallback)
            {
                this.resultCallback = resultCallback;
                this.errorCallback = errorCallback;
            }
            public bool OnData(ArraySegment<byte> data, Action continuation)
            {
                // since we're just buffering, ignore the continuation. 
                // TODO: place an upper limit on the size of the buffer. 
                // don't want a client to take up all the RAM on our server! 
                buffer.Add(data);
                return false;
            }
            public void OnError(Exception error)
            {
                errorCallback(error);
            }

            public void OnEnd()
            {
                // turn the buffer into a string. 
                // 
                // (if this isn't what you want, you could skip 
                // this step and make the result callback accept 
                // List<ArraySegment<byte>> or whatever) 
                // 
                var str = buffer
                    .Select(b => Encoding.UTF8.GetString(b.Array, b.Offset, b.Count))
                    .Aggregate((result, next) => result + next);

                resultCallback(str);
            }
        } 
    }
}
