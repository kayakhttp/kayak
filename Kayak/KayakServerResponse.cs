using System;
using System.Collections.Generic;
using System.Linq;

namespace Kayak
{
    /// <summary>
    /// A simple implementation of `IKayakServerResponse`. Default status is "200 OK HTTP/1.0". An attempt
    /// to modify the response status or headers after the headers have been written to the socket will
    /// throw an exception.
    /// </summary>
    public class KayakServerResponse : IKayakServerResponse
    {
        ISocket socket;

        int statusCode;
        string reasonPhrase;
        string httpVersion;
        IDictionary<string, string> headers;

        bool statusLineAndHeadersSent;

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

        /// <summary>
        /// Constructs a `KayakServerResponse` that will write its data to the given `ISocket`.
        /// </summary>
        /// <param name="socket"></param>
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
            if (statusLineAndHeadersSent)
                throw new InvalidOperationException(
                    "Cannot change the value of this property the after status line and headers have been written to the socket.");
        }

        public IObservable<Unit> Write(byte[] buffer, int offset, int count)
        {
            var writeData = WriteInternal(new ArraySegment<byte>(buffer, offset, count)).AsCoroutine<Unit>();

            if (!statusLineAndHeadersSent)
                writeData = PrependWithWriteHeaders(writeData);

            return writeData;
        }

        public IObservable<Unit> WriteFile(string file)
        {
            // discarding number of bytes written. i imagine it is only of some value
            // if the peer closes the connection on us. ignoring this case for now.
            var writeFile = socket.WriteFile(file).Select(i => new Unit());

            if (!statusLineAndHeadersSent)
                writeFile = PrependWithWriteHeaders(writeFile);

            return writeFile;
        }

        IObservable<Unit> WriteHeaders()
        {
            return WriteInternal(new ArraySegment<byte>(this.CreateHeaderBuffer())).AsCoroutine<Unit>();
        }

        IObservable<Unit> PrependWithWriteHeaders(IObservable<Unit> write)
        {
            var writeHeadersFirst = WriteHeaders().Concat(write);

            return Observable.CreateWithDisposable<Unit>(o =>
            {
                statusLineAndHeadersSent = true;
                return writeHeadersFirst.Subscribe(o);
            });
        }

        IEnumerable<object> WriteInternal(ArraySegment<byte> bytes)
        {
            int bytesWritten = 0;

            while (bytesWritten < bytes.Count)
                yield return socket.Write(bytes.Array, bytes.Offset + bytesWritten, bytes.Count - bytesWritten)
                    .Do(n => bytesWritten += n);
        }

        public IObservable<Unit> End()
        {
            return EndInternal().AsCoroutine<Unit>();
        }

        IEnumerable<object> EndInternal()
        {
            if (!statusLineAndHeadersSent)
                yield return PrependWithWriteHeaders(Observable.Empty<Unit>());

            // always close connection, we only support HTTP/1.0
            socket.Dispose();
        }
    }
}
