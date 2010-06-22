using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Oars;
using System.IO;
using System.Threading;

namespace Kayak
{
    // return null and the server will ignore connection
    public interface IKayakContextFactory
    {
        IKayakContext CreateContext(IKayakServer server, ISocket socket);
    }

    // A single-element sequence. Unit is generated after headers are read
    // and the context is usable.
    public interface IKayakContext : IObservable<Unit>
    {
        object UserData { get; set; }
        IKayakServer Server { get; }
        ISocket Socket { get; }
        IKayakServerRequest Request { get; }
        IKayakServerResponse Response { get; }

        void Start();
        void End();
    }

    public class KayakContext : IKayakContext
    {
        public static int MaxHeaderLength = 1024 * 10;
        public static int BufferSize = 1024 * 4;

        public object UserData { get; set; }
        public IKayakServer Server { get; private set; }
        public ISocket Socket { get; private set; }
        public KayakServerRequest Request { get; private set; }
        public KayakServerResponse Response { get; private set; }

        ObserverCollection<Unit> observers;
        Stream stream;

        public KayakContext(ISocket socket)
        {
            Socket = socket;
            stream = socket.GetStream();
            observers = new ObserverCollection<Unit>();
            Response = new KayakServerResponse(this);
        }

        public void Start()
        {
            ReadRequestHeaders().AsCoroutine().Start();
        }

        IEnumerable<object> ReadRequestHeaders()
        {
            var buffer = new byte[BufferSize];
            var headerBuffer = new MemoryStream(BufferSize);
            int endOfHeaders = 0, bytesRead = 0;

            while (endOfHeaders == 0)
            {
                yield return stream.ReadAsync(buffer, 0, buffer.Length).Do(n => bytesRead = n);

                //Console.WriteLine("context read " + bytesRead);

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
                    break; // TODO error
            }

            try
            {
                headerBuffer.Position = 0;
                HttpRequestLine requestLine = headerBuffer.ReadHttpRequestLine();
                NameValueDictionary headers = headerBuffer.ReadHttpHeaders();
                headerBuffer.Dispose();

                Stream requestBody = null;
                var contentLength = headers.GetContentLength();
                if (contentLength > 0)
                {
                    var overlapLength = bytesRead - endOfHeaders;
                    // this first bit gets copied around a lot...
                    var overlap = new byte[overlapLength];
                    Buffer.BlockCopy(buffer, endOfHeaders, overlap, 0, overlapLength);
                    //Console.WriteLine("Creating body stream with overlap " + overlapLength + ", contentLength " + contentLength);
                    requestBody = new RequestStream(stream, overlap, contentLength);
                }

                Request = new KayakServerRequest(requestLine, headers, requestBody);
                //Console.WriteLine("Going to yield a value.");
                observers.Next(new Unit());
                //Console.WriteLine("Yielded a value.");
            }
            catch (Exception e)
            {
                observers.Error(e);
            }
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
            return new ResponseStream(stream, GetHeaderBuffer(), Response.Headers.GetContentLength());
        }

        public void End()
        {
            if (responseBody)
                observers.Completed();
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
            //Console.WriteLine("Wrote headers.");
            observers.Completed();
        }

        IKayakServerRequest IKayakContext.Request
        {
            get { return (IKayakServerRequest)Request; }
        }

        IKayakServerResponse IKayakContext.Response
        {
            get { return (IKayakServerResponse)Response; }
        }

        public IDisposable Subscribe(IObserver<Unit> observer)
        {
            return observers.Add(observer);
        }

        class KayakContextFactory : IKayakContextFactory
        {
            public IKayakContext CreateContext(IKayakServer server, ISocket socket)
            {
                var c = new KayakContext(socket);
                c.Server = server;
                return c;
            }
        }

        static KayakContextFactory defaultFactory;

        internal static IKayakContextFactory DefaultFactory
        {
            get
            {
                return defaultFactory ?? (defaultFactory = new KayakContextFactory());
            }
        }

    }
}
