using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Kayak.Tests.Http
{
    class TransactionTestCases
    {
        public string Name;
        public IEnumerable<RequestInfo> Requests;
        public IEnumerable<ResponseInfo> UserResponses;
        public IEnumerable<ResponseInfo> ExpectedResponses;

        public override string ToString()
        {
            return Name;
        }

        public static IEnumerable<TransactionTestCases> GetValues()
        {
            //////////////// HTTP/1.0

            yield return new TransactionTestCases()
            {
                Name = "1.0 request no body response no body",
                Requests = new[] { Request.OneOhNoBody },
                UserResponses = new[] { Response.TwoHundredOKNoBody },
                ExpectedResponses = new[] { Response.TwoHundredOKConnectionCloseNoBody }
            };

            yield return new TransactionTestCases()
            {
                Name = "1.0 request no body response with body",
                Requests = new[] { Request.OneOhNoBody },
                UserResponses = new[] { Response.TwoHundredOKWithBody },
                ExpectedResponses = new[] { Response.TwoHundredOKConnectionCloseWithBody }
            };

            yield return new TransactionTestCases()
            {
                Name = "1.0 request with body response with body",
                Requests = new[] { Request.OneOhWithBody },
                UserResponses = new[] { Response.TwoHundredOKWithBody },
                ExpectedResponses = new[] { Response.TwoHundredOKConnectionCloseWithBody }
            };

            yield return new TransactionTestCases()
            {
                Name = "1.0 request no body response no body 2x",
                Requests = new[] { Request.OneOhKeepAliveNoBody, Request.OneOhNoBody },
                UserResponses = new[] { Response.TwoHundredOKNoBody, Response.TwoHundredOKNoBody },
                ExpectedResponses = new[] { Response.TwoHundredOKNoBody, Response.TwoHundredOKConnectionCloseNoBody }
            };

            yield return new TransactionTestCases()
            {
                Name = "1.0 request with body response with body 2x",
                Requests = new[] { Request.OneOhKeepAliveWithBody, Request.OneOhWithBody },
                UserResponses = new[] { Response.TwoHundredOKWithBody, Response.TwoHundredOKWithBody },
                ExpectedResponses = new[] { Response.TwoHundredOKWithBody, Response.TwoHundredOKConnectionCloseWithBody }
            };

            //////////////// HTTP/1.1

            yield return new TransactionTestCases()
            {
                Name = "1.1 request no body response no body",
                Requests = new[] { Request.OneOneNoBody },
                UserResponses = new[] { Response.TwoHundredOKNoBody },
                ExpectedResponses = new[] { Response.TwoHundredOKNoBody }
            };

            yield return new TransactionTestCases()
            {
                Name = "1.1 request connection close no body response connection close no body",
                Requests = new[] { Request.OneOneConnectionCloseNoBody },
                UserResponses = new[] { Response.TwoHundredOKNoBody },
                ExpectedResponses = new[] { Response.TwoHundredOKConnectionCloseNoBody }
            };

            yield return new TransactionTestCases()
            {
                Name = "1.1 request no body connection close no body response connection close no body",
                Requests = new[] { Request.OneOneNoBody, Request.OneOneConnectionCloseNoBody },
                UserResponses = new[] { Response.TwoHundredOKNoBody, Response.TwoHundredOKNoBody },
                ExpectedResponses = new[] { Response.TwoHundredOKNoBody, Response.TwoHundredOKConnectionCloseNoBody }
            };

            ////// HTTP/1.1 without expect-continue

            yield return new TransactionTestCases()
            {
                Name = "1.1 request with body response no body",
                Requests = new[] { Request.OneOneWithBody },
                UserResponses = new[] { Response.TwoHundredOKNoBody },
                ExpectedResponses = new[] { Response.TwoHundredOKNoBody }
            };

            yield return new TransactionTestCases()
            {
                Name = "1.1 request connection close with body response connection close no body",
                Requests = new[] { Request.OneOneConnectionCloseWithBody },
                UserResponses = new[] { Response.TwoHundredOKNoBody },
                ExpectedResponses = new[] { Response.TwoHundredOKConnectionCloseNoBody }
            };

            yield return new TransactionTestCases()
            {
                Name = "1.1 request no body connection close with body response connection close no body",
                Requests = new[] { Request.OneOneWithBody, Request.OneOneConnectionCloseWithBody },
                UserResponses = new[] { Response.TwoHundredOKNoBody, Response.TwoHundredOKNoBody },
                ExpectedResponses = new[] { Response.TwoHundredOKNoBody, Response.TwoHundredOKConnectionCloseNoBody }
            };

            yield return new TransactionTestCases()
            {
                Name = "1.1 request with body response with body",
                Requests = new[] { Request.OneOneWithBody },
                UserResponses = new[] { Response.TwoHundredOKWithBody },
                ExpectedResponses = new[] { Response.TwoHundredOKWithBody }
            };

            yield return new TransactionTestCases()
            {
                Name = "1.1 request connection close with body response connection close with body",
                Requests = new[] { Request.OneOneConnectionCloseWithBody },
                UserResponses = new[] { Response.TwoHundredOKWithBody },
                ExpectedResponses = new[] { Response.TwoHundredOKConnectionCloseWithBody }
            };

            yield return new TransactionTestCases()
            {
                Name = "1.1 request no body connection close with body response connection close with body",
                Requests = new[] { Request.OneOneWithBody, Request.OneOneConnectionCloseWithBody },
                UserResponses = new[] { Response.TwoHundredOKWithBody, Response.TwoHundredOKWithBody },
                ExpectedResponses = new[] { Response.TwoHundredOKWithBody, Response.TwoHundredOKConnectionCloseWithBody }
            };

            //////// HTTP/1.1 with expect-continue

            yield return new TransactionTestCases()
            {
                Name = "1.1 request expect-continue with body response no body",
                Requests = new[] { Request.OneOneExpectContinueWithBody },
                UserResponses = new[] { Response.TwoHundredOKNoBody },
                ExpectedResponses = new[] { Response.TwoHundredOKNoBody }
            };

            yield return new TransactionTestCases()
            {
                Name = "1.1 request connection close expect-continue with body response connection close no body",
                Requests = new[] { Request.OneOneConnectionCloseExpectContinueWithBody },
                UserResponses = new[] { Response.TwoHundredOKNoBody },
                ExpectedResponses = new[] { Response.TwoHundredOKConnectionCloseNoBody }
            };

            yield return new TransactionTestCases()
            {
                Name = "1.1 request connection close expect-continue with body response connection close no body",
                Requests = new[] { Request.OneOneExpectContinueWithBody, Request.OneOneConnectionCloseExpectContinueWithBody },
                UserResponses = new[] { Response.TwoHundredOKNoBody, Response.TwoHundredOKNoBody },
                ExpectedResponses = new[] { Response.TwoHundredOKNoBody, Response.TwoHundredOKConnectionCloseNoBody }
            };

            yield return new TransactionTestCases()
            {
                Name = "1.1 request expect-continue with body response with body",
                Requests = new[] { Request.OneOneExpectContinueWithBody },
                UserResponses = new[] { Response.TwoHundredOKWithBody },
                ExpectedResponses = new[] { Response.TwoHundredOKWithBody }
            };

            yield return new TransactionTestCases()
            {
                Name = "1.1 request connection close expect-continue with body response connection close with body",
                Requests = new[] { Request.OneOneConnectionCloseExpectContinueWithBody },
                UserResponses = new[] { Response.TwoHundredOKWithBody },
                ExpectedResponses = new[] { Response.TwoHundredOKConnectionCloseWithBody }
            };

            yield return new TransactionTestCases()
            {
                Name = "1.1 request connection close expect-continue with body response connection close with body",
                Requests = new[] { Request.OneOneExpectContinueWithBody, Request.OneOneConnectionCloseExpectContinueWithBody },
                UserResponses = new[] { Response.TwoHundredOKWithBody, Response.TwoHundredOKWithBody },
                ExpectedResponses = new[] { Response.TwoHundredOKWithBody, Response.TwoHundredOKConnectionCloseWithBody }
            };
        }
    }
}
