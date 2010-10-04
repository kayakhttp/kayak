using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Kayak.Core;

namespace Kayak
{
    public class KayakResponse : IHttpServerResponse
    {
        HttpStatusLine statusLine;
        IDictionary<string,string> headers;
        string bodyFile;
        IEnumerable<IObservable<ArraySegment<byte>>> getBody;

        public int StatusCode
        {
            get { return statusLine.StatusCode; }
        }

        public string ReasonPhrase
        {
            get { return statusLine.ReasonPhrase; }
        }

        public string HttpVersion
        {
            get { return "HTTP/1.0"; }
        }

        public IDictionary<string, string> Headers
        {
            get { return headers; }
        }

        public virtual string BodyFile
        {
            get { return bodyFile; }
        }

        public IEnumerable<IObservable<ArraySegment<byte>>> GetBody()
        {
            return getBody;
        }

        public KayakResponse(HttpStatusLine statusLine, IDictionary<string, string> headers) 
            : this(statusLine, headers, null, null) { }

        public KayakResponse(HttpStatusLine statusLine, IDictionary<string, string> headers, string bodyFile)
            : this(statusLine, headers, bodyFile, null) { }

        public KayakResponse(HttpStatusLine statusLine, IDictionary<string, string> headers,
            IEnumerable<IObservable<ArraySegment<byte>>> getBody)
            : this(statusLine, headers, null, getBody) { }

        KayakResponse(HttpStatusLine statusLine, IDictionary<string, string> headers, string bodyFile, IEnumerable<IObservable<ArraySegment<byte>>> getBody)
        {
            this.statusLine = statusLine;
            this.headers = headers;
            this.bodyFile = bodyFile;
            this.getBody = getBody;
        }
    }
}
