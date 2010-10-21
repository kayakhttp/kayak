using System.Collections.Generic;
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

        public KayakResponse(string statusLine, IDictionary<string, string> headers, params object[] body)
            : this(statusLine, headers, (IEnumerable<object>)body) { }

        public KayakResponse(string statusLine, IDictionary<string, string> headers, IEnumerable<object> getBody)
        {
            this.statusLine = statusLine;
            this.headers = headers;
            this.getBody = getBody;
        }
    }
}
