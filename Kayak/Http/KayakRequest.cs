using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Owin;

namespace Kayak
{
    public class KayakRequest : IRequest
    {
        public string Method { get; set; }
        public string Uri { get; set; }
        public IDictionary<string, IEnumerable<string>> Headers { get; private set; }
        public IDictionary<string, object> Items { get; private set; }

        ISocket socket;
        ArraySegment<byte> bodyDataReadWithHeaders;
        IAsyncResult currentResult;

        public static IRequest CreateRequest(ISocket socket, LinkedList<ArraySegment<byte>> headerBuffers)
        {
            var bodyDataReadWithHeaders = headerBuffers.Last.Value;
            headerBuffers.RemoveLast();
            
            var headersString = headerBuffers.GetString();
            var reader = new StringReader(headersString);
            var requestLine = reader.ReadRequestLine();
            var headers = reader.ReadHeaders();
            var context = new Dictionary<string, object>();

            return new KayakRequest(socket, requestLine, headers, context, bodyDataReadWithHeaders);
        }

        public KayakRequest(ISocket socket, HttpRequestLine requestLine, IDictionary<string, IEnumerable<string>> headers, IDictionary<string, object> context, ArraySegment<byte> bodyDataReadWithHeaders)
        {
            this.socket = socket;
            Method = requestLine.Verb;
            Uri = requestLine.RequestUri;
            Headers = headers;
            Items = context;
            this.bodyDataReadWithHeaders = bodyDataReadWithHeaders;
        }

        IEnumerator<Func<ArraySegment<byte>, IObservable<int>>> bodyEnumerator;

        public IAsyncResult BeginReadBody(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
        {
            if (currentResult != null)
                throw new InvalidOperationException("Async operation is already pending.");

            if (callback == null)
                throw new ArgumentNullException("callback");

            if (bodyDataReadWithHeaders.Count > 0)
            {
                int bytesRead;
                var result = new AsyncResult<int>(callback, state);

                bytesRead = Math.Min(bodyDataReadWithHeaders.Count, count);
                Buffer.BlockCopy(bodyDataReadWithHeaders.Array, bodyDataReadWithHeaders.Offset, buffer, offset, bytesRead);

                if (bytesRead < bodyDataReadWithHeaders.Count)
                    bodyDataReadWithHeaders =
                        new ArraySegment<byte>(
                            bodyDataReadWithHeaders.Array,
                            bodyDataReadWithHeaders.Offset + bytesRead,
                            bodyDataReadWithHeaders.Count - bytesRead);
                else
                    bodyDataReadWithHeaders = default(ArraySegment<byte>);

                currentResult = result;
                result.SetAsCompleted(bytesRead, true);
            }
            else if (socket != null)
            {
                var tcs = new TaskCompletionSource<int>();

                currentResult = tcs.Task; 

                socket.Read(buffer, offset, count).ContinueWith(t =>
                {
                    if (t.IsFaulted)
                        tcs.SetException(t.Exception);
                    else
                        tcs.SetResult(t.Result);

                    callback(tcs.Task);
                });

            }
            else
            {
                var result = new AsyncResult<int>(callback, state);
                currentResult = result;
                result.SetAsCompleted(0, true);
            }

            return currentResult;
        }

        public int EndReadBody(IAsyncResult result)
        {
            if (result == null)
                throw new ArgumentNullException("result");
            
            if (currentResult == null)
                throw new InvalidOperationException("No async operation pending.");

            if (result != currentResult)
                throw new ArgumentException("Invalid IAsyncResult");

            currentResult = null;

            if (result is AsyncResult<int>)
                return (result as AsyncResult<int>).EndInvoke();
            else if (result is Task<int>)
                return (result as Task<int>).Result;
            else
                throw new ArgumentException("Invalid IAsyncResult");
        }
    }
}
