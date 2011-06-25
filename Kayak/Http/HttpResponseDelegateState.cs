using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Kayak.Http
{
    class HttpResponseDelegateState
    {
        bool gotContinue;
        bool gotResponse;
        bool gotConnect;
        bool gotAbort;
        bool shouldKeepAlive;
        bool prohibitBody;

        public HttpResponseDelegateState(bool prohibitBody, bool shouldKeepAlive)
        {
            this.prohibitBody = prohibitBody;
            this.shouldKeepAlive = shouldKeepAlive;
        }

        // called when user connects to request body.
        public void OnWriteContinue(out bool writeContinue)
        {
            if (gotAbort)
                throw new InvalidOperationException("The response was aborted by the server.");

            gotContinue = true;
            
            // if we already have a response, don't send 100-continue, just send the response
            writeContinue = gotConnect && !gotResponse;
        }

        public void OnResponse(bool hasBody, out bool writeResponse)
        {
            if (gotAbort)
                throw new InvalidOperationException("The response was aborted by the server.");

            if (gotResponse)
                throw new InvalidOperationException("OnResponse was already called.");

            gotResponse = true;

            if (prohibitBody && hasBody)
                throw new InvalidOperationException("Response body is prohibited for this response type.");

            writeResponse = gotConnect;
        }

        public void OnConnect(out bool writeResponse, out bool writeContinue)
        {
            if (gotConnect)
                throw new InvalidOperationException("Connect was already called.");

            gotConnect = true;
            writeContinue = /* !gotResponse && */ gotContinue;
            writeResponse = gotResponse;
        }

        public void OnRenderHeaders(bool givenConnectionHeader, bool givenConnectionHeaderIsClose, out bool indicateConnection, out bool indicateConnectionClose)
        {
            // XXX incorporate request version
            if (!shouldKeepAlive || givenConnectionHeaderIsClose)
            {
                indicateConnection = true;
                indicateConnectionClose = true;
            }
            else
            {
                indicateConnection = true;
                indicateConnectionClose = false;
            }
        }

        public void OnAbort()
        {
            gotAbort = true;
        }
    }
}
