using System.Collections.Generic;
using System.Linq;
using Owin;
using System.Text;

namespace Kayak
{
    public class KayakResponse : IResponse
    {
        string statusLine;
        IDictionary<string, IEnumerable<string>> headers;
        IEnumerable<object> body;

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
            return body;
        }

        protected void SetBody(IEnumerable<object> body)
        {
            this.body = body;
        }

        public KayakResponse(string statusLine)
            : this(statusLine, Enumerable.Empty<object>()) { }

        public KayakResponse(string statusLine, IEnumerable<object> body)
            : this(statusLine, new Dictionary<string, IEnumerable<string>>(), body) { }

        public KayakResponse(string statusLine, params object[] body)
            : this(statusLine, new Dictionary<string, IEnumerable<string>>(), body) { }

        public KayakResponse(string statusLine, IDictionary<string, IEnumerable<string>> headers, params object[] body)
            : this(statusLine, headers, (IEnumerable<object>)body) { }

        public KayakResponse(string statusLine, IDictionary<string, IEnumerable<string>> headers, IEnumerable<object> body)
        {
            this.statusLine = statusLine;
            this.headers = headers;
            this.body = body.Select(o => o is string ? Encoding.ASCII.GetBytes(o as string) : o);
        }
    }
}
