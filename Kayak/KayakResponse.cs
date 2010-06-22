using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using Oars;

namespace Kayak
{
    public interface IKayakServerResponse
    {
        int StatusCode { get; set; }
        string ReasonPhrase { get; set; }
        string HttpVersion { get; set; }
        NameValueDictionary Headers { get; set; }
        Stream Body { get; }
    }

    public class KayakServerResponse : IKayakServerResponse
    {
        internal HttpStatusLine statusLine;
        NameValueDictionary headers;
        KayakContext context;
        Stream body;

        /// <summary>
        /// Gets or sets the HTTP status code (e.g., 200, 404, 304, etc.) to be sent with the response. An exception
        /// will be thrown if this property is set after the headers have been sent.
        /// </summary>
        public int StatusCode
        {
            get { return statusLine.StatusCode; }
            set { ThrowIfBodyAccessed(); statusLine.StatusCode = value; }
        }

        /// <summary>
        /// Gets or sets the HTTP status description (e.g., "OK", "Not Found", etc.) to be sent with the response. An exception
        /// will be thrown if this property is set after the headers have been sent.
        /// </summary>
        public string ReasonPhrase
        {
            get { return statusLine.ReasonPhrase; }
            set { ThrowIfBodyAccessed(); statusLine.ReasonPhrase = value; }
        }

        public string HttpVersion
        {
            get { return statusLine.HttpVersion; }
            set { throw new InvalidOperationException("Kayak only supports " + statusLine.HttpVersion); }
        }

        public NameValueDictionary Headers { 
            get { return headers; }
            set { ThrowIfBodyAccessed(); headers = value; } 
        }

        public Stream Body { 
            get {
                if (body == null)
                    body = context.GetResponseStream();

                return body;
            } 
        }

        public KayakServerResponse(KayakContext context)
        {
            this.context = context;
            statusLine = new HttpStatusLine()
            {
                StatusCode = 200,
                ReasonPhrase = "OK",
                HttpVersion = "HTTP/1.1"
            };
            headers = new NameValueDictionary();
        }
        
        void ThrowIfBodyAccessed()
        {
            if (body != null)
                throw new InvalidOperationException("Headers have already been sent to the client, cannot modify or send again.");
        }
    }
}
