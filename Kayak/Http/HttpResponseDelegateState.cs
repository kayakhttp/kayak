using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Kayak.Http
{
    class HttpResponseDelegateState
    {
        bool gotResponse;
        bool gotConnect;
        bool gotAbort;
        bool shouldKeepAlive;
        bool prohibitBody;
        bool expectContinue;

        public static HttpResponseDelegateState Create(bool prohibitBody, bool shouldKeepAlive, bool expectContinue)
        {
            return new HttpResponseDelegateState()
            {
                prohibitBody = prohibitBody,
                shouldKeepAlive = shouldKeepAlive,
                expectContinue = expectContinue
            };
        }

        public void OnResponse(bool hasBody, out bool begin)
        {
            if (gotAbort)
                throw new InvalidOperationException("The response was aborted by the server.");

            if (gotResponse)
                throw new InvalidOperationException("OnResponse was already called.");

            gotResponse = true;

            if (prohibitBody && hasBody)
                throw new InvalidOperationException("Response body is prohibited for this response type.");

            begin = gotConnect;
        }

        public void OnConnect(out bool begin)
        {
            if (gotConnect)
                throw new InvalidOperationException("Connect was already called.");

            gotConnect = true;
            begin = gotResponse;
        }

        public void OnRenderHeaders(bool givenConnectionHeader, bool givenConnectionHeaderIsClose, out bool indicateConnection, out bool indicateConnectionClose)
        {
            // XXX incorporate request version
            if (!shouldKeepAlive)
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
