using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HttpMachine;
using System.Diagnostics;

namespace Kayak.Http
{
    // transforms socket events into http server transaction events.
    class HttpServerSocketDelegate : ISocketDelegate
    {
        HttpParser parser;
        ParserToTransactionTransform transactionTransform;
        IHttpServerTransactionDelegate transactionDelegate;

        public HttpServerSocketDelegate(IHttpServerTransactionDelegate transactionDelegate)
        {
            this.transactionDelegate = transactionDelegate;
            transactionTransform = new ParserToTransactionTransform(transactionDelegate);
            parser = new HttpParser(new ParserDelegate(transactionTransform));
        }

        public bool OnData(ISocket socket, ArraySegment<byte> data, Action continuation)
        {
            try
            {
                var parsed = parser.Execute(data);

                if (parsed != data.Count)
                {
                    Trace.Write("Error while parsing request.");
                    throw new Exception("Error while parsing request.");
                }

                // raises request events on transaction delegate
                return transactionTransform.Commit(continuation);
            }
            catch (Exception e)
            {
                OnError(socket, e);
                OnClose(socket);
                throw;
            }
        }

        public void OnEnd(ISocket socket)
        {
            Debug.WriteLine("Socket OnEnd.");

            // parse EOF
            OnData(socket, default(ArraySegment<byte>), null);

            transactionDelegate.OnEnd();
        }

        public void OnError(ISocket socket, Exception e)
        {
            Debug.WriteLine("Socket OnError.");
            e.DebugStacktrace();
            transactionDelegate.OnError(e);
        }

        public void OnClose(ISocket socket)
        {
            Debug.WriteLine("Socket OnClose.");
            transactionDelegate.OnClose();
        }

        public void OnConnected(ISocket socket)
        {
            throw new NotImplementedException();
        }
    }
}
