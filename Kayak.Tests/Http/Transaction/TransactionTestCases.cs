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

            yield return new TransactionTestCases()
            {
                Name = "1.1 single request response",
                Requests = new[] { Request.OneOneNoBody },
                UserResponses = new[] { Response.TwoHundredOKWithBody },
                ExpectedResponses = new[] { Response.TwoHundredOKWithBody }
            };

            yield return new TransactionTestCases()
            {
                Name = "1.1 two request response",
                Requests = new[] { Request.OneOneNoBody, Request.OneOneNoBody },
                UserResponses = new[] { Response.TwoHundredOKWithBody, Response.TwoHundredOKWithBody },
                ExpectedResponses = new[] { Response.TwoHundredOKWithBody, Response.TwoHundredOKWithBody }
            };

            yield return new TransactionTestCases()
            {
                Name = "1.1 request with body response with body",
                Requests = new[] { Request.OneOneExpectContinueWithBody },
                UserResponses = new[] { Response.TwoHundredOKWithBody },
                ExpectedResponses = new[] { Response.TwoHundredOKWithBody }
            };
        }
    }
}
