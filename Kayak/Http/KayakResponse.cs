using System.Collections.Generic;
using Owin;
using System.Collections;

namespace Kayak
{
    public class KayakResponse : IResponse
    {
        string statusLine;
        IDictionary<string,IEnumerable<string>> headers;
        IEnumerable<object> getBody;

        public string Status
        {
            get { return statusLine; }
        }

        public IDictionary<string, IEnumerable<string>> Headers
        {
            get { return headers; }
        }

        public IEnumerable<object> GetBody()
        {
            return getBody;
        }

        public KayakResponse(string statusLine, IDictionary<string, IEnumerable<string>> headers, params object[] body)
            : this(statusLine, headers, (IEnumerable<object>)body) { }

        public KayakResponse(string statusLine, IDictionary<string, IEnumerable<string>> headers, IEnumerable<object> getBody)
        {
            this.statusLine = statusLine;
            this.headers = headers;
            this.getBody = getBody;
        }
    }
}
