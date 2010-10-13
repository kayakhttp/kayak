using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Kayak.Core;

namespace Kayak
{
    public class KayakResponse : IHttpServerResponse
    {
        string statusLine;
        IDictionary<string,string> headers;
        IEnumerable<object> getBody;

        public string Status
        {
            get { return statusLine; }
        }

        public IDictionary<string, string> Headers
        {
            get { return headers; }
        }

        public IEnumerable<object> GetBody()
        {
            return getBody;
        }

        public KayakResponse(HttpStatusLine statusLine, IDictionary<string, string> headers, IEnumerable<object> getBody)
            : this(statusLine.StatusCode + " " + statusLine.ReasonPhrase, headers, getBody) { }

        public KayakResponse(string statusLine, IDictionary<string, string> headers, IEnumerable<object> getBody)
        {
            this.statusLine = statusLine;
            this.headers = headers;
            this.getBody = getBody;
        }
    }
}
