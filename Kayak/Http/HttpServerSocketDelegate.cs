using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HttpMachine;
using System.Diagnostics;

namespace Kayak.Http
{
    // transforms socket events into http server transaction events.
    class HttpServerSocketDelegate
    {
        ISocket socket;
        HttpParser parser;
        ParserToTransactionTransform del;
        IHttpServerTransactionDelegate transactionDelegate;

        public HttpServerSocketDelegate(ISocket socket, IHttpServerTransactionDelegate transactionDelegate)
        {
            this.transactionDelegate = transactionDelegate;
            Attach(socket);
            del = new ParserToTransactionTransform(transactionDelegate);
            parser = new HttpParser(new ParserDelegate(del));
        }

        bool OnData(ArraySegment<byte> data, Action continuation)
        {
            var parsed = parser.Execute(data);

            if (parsed != data.Count)
            {
                Trace.Write("Error while parsing request.");

                Detach();

                // XXX what to do with this?
                throw new Exception("Error while parsing request.");
            }

            return del.Commit(continuation);
        }

        void OnEnd()
        {
            Debug.WriteLine("Socket OnEnd.");

            // parse EOF
            OnData(default(ArraySegment<byte>), null);

            transactionDelegate.OnEnd();
        }

        void OnClose()
        {
            Debug.WriteLine("Socket OnClose.");
            Detach();
        }

        void OnError(Exception e)
        {
            Debug.WriteLine("Socket OnError.");
            e.PrintStacktrace();
        }

        void Attach(ISocket socket)
        {
            this.socket = socket;
            socket.OnData += socket_OnData;
            socket.OnEnd += socket_OnEnd;
            socket.OnError += socket_OnError;
            socket.OnClose += socket_OnClose;
        }

        void Detach()
        {
            socket.OnData -= socket_OnData;
            socket.OnEnd -= socket_OnEnd;
            socket.OnError -= socket_OnError;
            socket.OnClose -= socket_OnClose;
            socket.Dispose();
        }

        #region Socket Event Boilerplate

        void socket_OnClose(object sender, EventArgs e)
        {
            OnClose();
        }

        void socket_OnError(object sender, ExceptionEventArgs e)
        {
            OnError(e.Exception);
        }

        void socket_OnEnd(object sender, EventArgs e)
        {
            OnEnd();
        }

        void socket_OnData(object sender, DataEventArgs e)
        {
            e.WillInvokeContinuation = OnData(e.Data, e.Continuation);
        }

        #endregion
    }
}
