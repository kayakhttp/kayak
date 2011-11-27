using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Kayak.Http;

namespace Kayak.Tests.Http
{
    class RequestCallbacker : User
    {
        public Action<UserKayak> OnRequestAction;
        public Action<UserKayak> OnRequestBodyEndAction;
        public Action<UserKayak> ConnectResponseBodyAction;

        public void OnRequest(UserKayak kayak, HttpRequestHead head)
        {
            if (OnRequestAction != null)
                OnRequestAction(kayak);
        }

        public void OnRequestBodyData(UserKayak kayak, string data)
        {
        }

        public void OnRequestBodyError(UserKayak kayak, Exception error)
        {
        }

        public void OnRequestBodyEnd(UserKayak kayak)
        {
            if (OnRequestBodyEndAction != null)
                OnRequestBodyEndAction(kayak);
        }

        public void ConnectResponseBody(UserKayak kayak)
        {
            if (ConnectResponseBodyAction != null)
                ConnectResponseBodyAction(kayak);
        }

        public void DisconnectResponseBody(UserKayak kayak)
        {
        }
    }
}
