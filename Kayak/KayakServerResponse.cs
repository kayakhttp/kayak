using System;
using System.IO;
using System.Collections.Generic;

namespace Kayak
{
    /// <summary>
    /// A simple implementation of `IKayakServerResponse`. Default status is "200 OK HTTP/1.0". An attempt
    /// to modify the response status or headers after the headers have been written to the socket will
    /// throw an exception.
    /// </summary>
    public class KayakServerResponse : IKayakServerResponse
    {
        bool headersSent;
        int statusCode;
        string reasonPhrase;
        string httpVersion;
        ResponseStream body;
        IDictionary<string, string> headers;
        ISocket socket;

        public int StatusCode
        {
            get { return statusCode; }
            set { ThrowIfHeadersSent(); statusCode = value; }
        }

        public string ReasonPhrase
        {
            get { return reasonPhrase; }
            set { ThrowIfHeadersSent(); reasonPhrase = value; }
        }

        public string HttpVersion
        {
            get { return httpVersion; }
            set { ThrowIfHeadersSent(); httpVersion = value; }
        }

        public IDictionary<string, string> Headers { 
            get { return headers; }
            set { ThrowIfHeadersSent(); headers = value; } 
        }

        public ResponseStream Body { 
            get {
                if (body == null)
                    body = this.CreateResponseStream(socket, () => {
                        headersSent = true;
                        return this.CreateHeaderBuffer();
                    });

                return body;
            } 
        }

        public KayakServerResponse(ISocket socket)
        {
            this.socket = socket;
            statusCode = 200;
            reasonPhrase = "OK";
            httpVersion = "HTTP/1.0";
            headers = new Dictionary<string,string>();
        }
        
        void ThrowIfHeadersSent()
        {
            if (headersSent)
                throw new InvalidOperationException("Headers have already been sent to the client and cannot be modified.");
        }
    }
}
