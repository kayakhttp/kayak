using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Kayak
{
    public interface IKayakContext : ISubject<Unit>
    {
        ISocket Socket { get; }
        IKayakServerRequest Request { get; }
        IKayakServerResponse Response { get; }
        Dictionary<object, object> Items { get; }
    }

    public class KayakContext : IKayakContext
    {
        public static int MaxHeaderLength = 1024 * 10;
        public static int BufferSize = 1024 * 2;

        public Dictionary<object, object> Items { get; private set; }
        public ISocket Socket { get; private set; }
        public KayakServerRequest Request { get; private set; }
        public KayakServerResponse Response { get; private set; }

        IObserver<Unit> observer;
        Stream stream;

        public KayakContext(ISocket socket)
        {
            Socket = socket;
            stream = socket.GetStream();
            Response = new KayakServerResponse(this);
            Items = new Dictionary<object, object>();
        }

        public void OnNext(Unit value)
        {
            observer.OnNext(value);
        }

        public void OnError(Exception e)
        {
            Console.WriteLine("Context error!");
            Console.Out.WriteException(e);
            stream.Close();
            observer.OnError(e);
        }

        public void Complete()
        {
            stream.Flush();
            stream.Close();
            stream.Dispose();

            observer.OnCompleted();
        }

        public IDisposable Subscribe(IObserver<Unit> observer)
        {
            var readRequestHeaders = ReadRequestHeaders().AsCoroutine();
            this.observer = observer;

            return readRequestHeaders.Subscribe(o => { }, OnError, () => { OnNext(new Unit()); });
        }

        IEnumerable<object> ReadRequestHeaders()
        {
            var buffer = new byte[BufferSize];
            var headerBuffer = new MemoryStream(BufferSize);
            int endOfHeaders = 0, bytesRead = 0;

            while (endOfHeaders == 0)
            {
                Trace.Write("Context about to read header chunk.");
                yield return stream.ReadAsync(buffer, 0, buffer.Length).Do(n => bytesRead = n);
                Trace.Write("Context read {0} bytes.", bytesRead);

                if (bytesRead == 0)
                    break;

                // looking for CRLF CRLF
                for (int i = 0; i < bytesRead; i++)
                {
                    if (buffer[i] == 10 && buffer[i - 1] == 13 &&
                        buffer[i - 2] == 10 && buffer[i - 3] == 13)
                    {
                        endOfHeaders = i + 1;
                        break;
                    }
                }

                // write up to and including CRLF CRLF into the header buffer
                headerBuffer.Write(buffer, 0, endOfHeaders > 0 ? endOfHeaders : bytesRead);

                if (headerBuffer.Length > MaxHeaderLength)
                    throw new Exception("Request headers data exceeds max header length.");
            }
            HttpRequestLine requestLine = default(HttpRequestLine);
            NameValueDictionary headers = null;

            headerBuffer.Position = 0;
            requestLine = headerBuffer.ReadHttpRequestLine();
            headers = headerBuffer.ReadHttpHeaders();
            headerBuffer.Dispose();
        
            Stream requestBody = null;
            var contentLength = headers.GetContentLength();
            Trace.Write("Got request with content length " + contentLength);
            if (contentLength > 0)
            {
                var overlapLength = bytesRead - endOfHeaders;
                // this first bit gets copied around a lot...
                var overlap = new byte[overlapLength];
                Buffer.BlockCopy(buffer, endOfHeaders, overlap, 0, overlapLength);
                Trace.Write("Creating body stream with overlap " + overlapLength + ", contentLength " + contentLength);
                requestBody = new RequestStream(stream, overlap, contentLength);
            }

            Request = new KayakServerRequest(requestLine, headers, requestBody);
        }

        bool responseBody;

        byte[] GetHeaderBuffer()
        {
            MemoryStream headerStream = new MemoryStream();
            headerStream.WriteHttpStatusLine(Response.statusLine);
            headerStream.WriteHttpHeaders(Response.Headers);
            headerStream.Position = 0;
            return headerStream.ToArray();
        }

        internal Stream GetResponseStream()
        {
            responseBody = true;
            var headerBuffer = GetHeaderBuffer();
            //Console.WriteLine("KayakContext: creating ResponseStream with {0} bytes of headers.", headerBuffer.Length);
            return new ResponseStream(stream, headerBuffer, Response.Headers.GetContentLength());
        }

        public void OnCompleted()
        {
            if (responseBody)
                Complete();
            else
            {
                //Console.WriteLine("Simply writing headers.");
                var headerBuffer = GetHeaderBuffer();
                stream.BeginWrite(headerBuffer, 0, headerBuffer.Length, WroteHeaders, null);
            }
        }

        public void WroteHeaders(IAsyncResult iasr)
        {
            stream.EndWrite(iasr);
            Complete();
        }

        IKayakServerRequest IKayakContext.Request
        {
            get { return (IKayakServerRequest)Request; }
        }

        IKayakServerResponse IKayakContext.Response
        {
            get { return (IKayakServerResponse)Response; }
        }
    }
}
