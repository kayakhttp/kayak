using System;
using System.IO;
using System.Collections.Generic;

namespace Kayak
{
    public class KayakServerResponse : IKayakServerResponse
    {
        int statusCode;
        string reasonPhrase;
        string httpVersion;
        ResponseStream body;
        NameValueDictionary headers;
        ISocket socket;

        /// <summary>
        /// Gets or sets the HTTP status code (e.g., 200, 404, 304, etc.) to be sent with the response. An exception
        /// will be thrown if this property is set after the headers have been sent.
        /// </summary>
        public int StatusCode
        {
            get { return statusCode; }
            set { ThrowIfBodyAccessed(); statusCode = value; }
        }

        /// <summary>
        /// Gets or sets the HTTP status description (e.g., "OK", "Not Found", etc.) to be sent with the response. An exception
        /// will be thrown if this property is set after the headers have been sent.
        /// </summary>
        public string ReasonPhrase
        {
            get { return reasonPhrase; }
            set { ThrowIfBodyAccessed(); reasonPhrase = value; }
        }

        public string HttpVersion
        {
            get { return httpVersion; }
            set { ThrowIfBodyAccessed(); httpVersion = value; }
        }

        public NameValueDictionary Headers { 
            get { return headers; }
            set { ThrowIfBodyAccessed(); headers = value; } 
        }

        public ResponseStream Body { 
            get {
                if (body == null)
                    body = this.CreateResponseStream(socket);

                return body;
            } 
        }

        public KayakServerResponse(ISocket socket)
        {
            this.socket = socket;
            statusCode = 200;
            reasonPhrase = "OK";
            httpVersion = "HTTP/1.0";
            headers = new NameValueDictionary();
        }
        
        void ThrowIfBodyAccessed()
        {
            if (body != null)
                throw new InvalidOperationException("Headers have already been sent to the client, cannot modify or send again.");
        }
    }
}
