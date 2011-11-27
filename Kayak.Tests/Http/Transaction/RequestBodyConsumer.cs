using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Kayak.Tests.Http
{
    class RequestBodyConsumer : IDataConsumer
    {
        User userCode;
        UserKayak kayak;

        public RequestBodyConsumer(User userCode, UserKayak kayak)
        {
            this.userCode = userCode;
            this.kayak = kayak;
        }

        public void OnError(Exception e)
        {
            userCode.OnRequestBodyError(kayak, e);
        }

        public bool OnData(ArraySegment<byte> data, Action continuation)
        {
            userCode.OnRequestBodyData(kayak, Encoding.ASCII.GetString(data.Array, data.Offset, data.Count));
            return false;
        }

        public void OnEnd()
        {
            userCode.OnRequestBodyEnd(kayak);
        }
    }
}
