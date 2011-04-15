using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Kayak.Http
{
    class ResponseState
    {
        bool shouldKeepAlive;
        public bool keepAlive;


        bool prohibitBody;
        bool expectContinue;
        bool sentContinue;
        bool wroteHeaders;
        bool renderedHeaders;
        bool ended;

        bool sentConnectionHeader;

        public static ResponseState Create(IHttpServerRequest request, bool shouldKeepAlive)
        {
            return new ResponseState()
            {
                prohibitBody = request.Method == "HEAD",
                shouldKeepAlive = shouldKeepAlive,
                expectContinue = request.IsContinueExpected()
            };
        }

        public void EnsureWriteHeaders()
        {
            if (ended)
                throw new InvalidOperationException("Response was ended.");

            if (wroteHeaders)
                throw new InvalidOperationException("Already wrote headers.");
        }

        public void OnWriteHeaders(bool prohibitBody)
        {
            if (expectContinue && !sentContinue)
                keepAlive = false;
        }

        public void EnsureWriteBody(out bool renderHeaders)
        {
            if (!wroteHeaders)
                throw new InvalidOperationException("Must call WriteHeaders before calling WriteBody.");
            if (prohibitBody)
                throw new InvalidOperationException("This type of response must not have a body.");
            if (ended)
                throw new InvalidOperationException("Response was ended.");

            renderHeaders = renderedHeaders ? false : (renderedHeaders = true);
        }

        public void OnRenderHeaders(bool givenConnectionHeader, bool givenConnectionHeaderIsClose, out bool indicateKeepAlive, out bool indicateClose)
        {
            indicateKeepAlive = false;
            indicateClose = false;

            if (givenConnectionHeader)
            {
                sentConnectionHeader = true;
                keepAlive = !givenConnectionHeaderIsClose;
            }
            else
            {
                if (shouldKeepAlive)
                    indicateKeepAlive = true;
                else
                {
                    indicateClose = true;
                    keepAlive = false;
                }
            }
        }

        public void OnWriteContinue()
        {
            sentContinue = true;
        }

        public void OnEnd(out bool renderHeaders)
        {
            if (ended)
                throw new InvalidOperationException("Response was ended.");

            ended = true;

            renderHeaders = renderedHeaders ? false : (renderedHeaders = true);
        }
    }
}
