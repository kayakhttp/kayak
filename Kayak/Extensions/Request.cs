using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Web;

namespace Kayak
{
    public static partial class Extensions
    {
        static int MaxHeaderLength = 1024 * 10;
        static int BufferSize = 1024 * 2;

        public static IObservable<KayakServerRequest> CreateRequest(this ISocket socket)
        {
            return CreateRequestInternal(socket).AsCoroutine<KayakServerRequest>();
        }

        static IEnumerable<object> CreateRequestInternal(ISocket socket)
        {
            var buffer = new byte[BufferSize];
            var headerBuffer = new MemoryStream(BufferSize);
            int endOfHeaders = 0, bytesRead = 0, rawBufferLength = 0;
            byte[] rawBuffer = null;

            while (endOfHeaders == 0)
            {
                Trace.Write("About to read header chunk.");
                yield return socket.Read(buffer, 0, buffer.Length).Do(n => bytesRead = n);
                Trace.Write("Read {0} bytes.", bytesRead);

                if (bytesRead == 0)
                    break;

                headerBuffer.Write(buffer, 0, bytesRead);

                rawBuffer = headerBuffer.GetBuffer();

                // looking for CRLF CRLF
                for (int i = (int)headerBuffer.Position - bytesRead; i < headerBuffer.Position; i++)
                {
                    if (rawBuffer[i] == 10 && rawBuffer[i - 1] == 13 &&
                        rawBuffer[i - 2] == 10 && rawBuffer[i - 3] == 13)
                    {
                        endOfHeaders = i + 1;
                        rawBufferLength = (int)headerBuffer.Position;
                        break;
                    }
                }

                if (headerBuffer.Length > MaxHeaderLength)
                    throw new Exception("Request headers data exceeds max header length.");
            }

            HttpRequestLine requestLine;
            NameValueDictionary headers;

            headerBuffer.Position = 0;
            headerBuffer.ReadHttpHeaders(out requestLine, out headers);

            RequestStream requestBody = null;
            var contentLength = headers.GetContentLength();
            Trace.Write("Got request with content length " + contentLength);

            if (contentLength > 0)
            {
                var overlapLength = (int)rawBufferLength - endOfHeaders;
                // this first bit gets copied around a lot...
                var overlap = new byte[overlapLength];
                Buffer.BlockCopy(rawBuffer, endOfHeaders, overlap, 0, overlapLength);
                Trace.Write("Creating body stream with overlap " + overlapLength + ", contentLength " + contentLength);
                requestBody = new RequestStream(socket, overlap, contentLength);
            }

            headerBuffer.Dispose();

            yield return new KayakServerRequest(requestLine, headers, requestBody);
        }

        public static string GetPath(this IKayakServerRequest request)
        {
            int question = request.RequestUri.IndexOf('?');
            return HttpUtility.UrlDecode(question >= 0 ? request.RequestUri.Substring(0, question) : request.RequestUri);
        }

        public static NameValueDictionary GetQueryString(this IKayakServerRequest request)
        {
            int question = request.RequestUri.IndexOf('?');
            return question >= 0 ?
                request.RequestUri.DecodeQueryString(question + 1, request.RequestUri.Length - question - 1) :
                new NameValueDictionary();
        }
    }
}
