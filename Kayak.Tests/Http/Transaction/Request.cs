using System;
using System.Collections.Generic;
using Kayak.Http;

namespace Kayak.Tests.Http
{
    class RequestInfo
    {
        public HttpRequestHead Head;
        public IEnumerable<string> Data;
        public Exception Exception;
    }

    class Request
    {
        public static RequestInfo OneOhKeepAliveWithBody = new RequestInfo()
        {
            Head = new HttpRequestHead()
            {
                Version = new Version(1, 0),
                Headers = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase) 
                        {
                            { "Connection", "keep-alive" }
                        }
            },
            Data = new[] { "hello ", "world." }
        };

        public static RequestInfo OneOhWithBody = new RequestInfo()
        {
            Head = new HttpRequestHead()
            {
                Version = new Version(1, 0),
                Headers = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase) 
                        {
                            { "X-Foo", "bar" }
                        }
            },
            Data = new[] { "hello ", "world!" }
        };

        public static RequestInfo OneOneNoBody = new RequestInfo()
        {
            Head = new HttpRequestHead()
            {
                Version = new Version(1, 1),
                Headers = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase) 
                        {
                            { "X-Foo", "bar" }
                        }
            }
        };

        public static RequestInfo OneOneExpectContinueWithBody = new RequestInfo()
        {
            Head = new HttpRequestHead()
            {
                Version = new Version(1, 1),
                Headers = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase) 
                        {
                            { "Expect", "100-continue" }
                        }
            },
            Data = new[] { "hello ", "world!" }
        };
    }
}
