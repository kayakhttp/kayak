using System;
using System.Collections.Generic;
using Kayak.Http;

namespace Kayak.Tests.Http
{
    class ResponseInfo
    {
        public HttpResponseHead Head;
        public IEnumerable<string> Data;
        public Exception Exception;
    }

    class Response
    {
        public static ResponseInfo OneHundredContinue = new ResponseInfo()
        {
            Head = new HttpResponseHead()
            {
                Status = "100 Continue"
            }
        };

        public static ResponseInfo TwoHundredOKNoBody = new ResponseInfo()
        {
            Head = new HttpResponseHead()
            {
                Status = "200 OK"
            }
        };

        public static ResponseInfo TwoHundredOKConnectionCloseNoBody = new ResponseInfo()
        {
            Head = new HttpResponseHead()
            {
                Status = "200 OK",
                Headers = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase)
                {
                    { "Connection", "close" }
                }
            }
        };

        public static ResponseInfo TwoHundredOKWithBody = new ResponseInfo()
        {
            Head = new HttpResponseHead()
            {
                Status = "200 OK"
            },
            Data = new[] { "yo ", "dawg." }
        };

        public static ResponseInfo TwoHundredOKConnectionCloseWithBody = new ResponseInfo()
        {
            Head = new HttpResponseHead()
            {
                Status = "200 OK",
                Headers = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase)
                {
                    { "Connection", "close" }
                }
            },
            Data = new[] { "yo ", "dawg." }
        };
    }

}
