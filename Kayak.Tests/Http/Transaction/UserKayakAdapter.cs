using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Kayak.Http;

namespace Kayak.Tests.Http
{
    class UserKayakAdapter : UserKayak
    {
        User userCode;
        IDisposable disconnect;
        IDataProducer requestBody;
        IHttpResponseDelegate responseDelegate;
        SimpleSubject subject;

        public UserKayakAdapter(User userCode, IDataProducer requestBody, IHttpResponseDelegate responseDelegate)
        {
            this.userCode = userCode;
            this.requestBody = requestBody;
            this.responseDelegate = responseDelegate;
        }

        public void ConnectRequestBody()
        {
            var consumer = new MockDataConsumer()
            {
                OnDataAction = data => userCode.OnRequestBodyData(this, Encoding.ASCII.GetString(data.Array, data.Offset, data.Count)),
                OnEndAction = () => userCode.OnRequestBodyEnd(this)
            };

            if (disconnect != null) throw new Exception("got connect and disconnect was not null");
            disconnect = requestBody.Connect(consumer);
        }

        public void DisconnectRequestBody()
        {
            disconnect.Dispose();
        }

        public void OnResponse(HttpResponseHead head)
        {
            subject = new SimpleSubject(
                () => userCode.ConnectResponseBody(this),
                () => userCode.DisconnectResponseBody(this));

            responseDelegate.OnResponse(head, subject);
        }

        public void OnResponseBodyData(string data)
        {
            subject.OnData(new ArraySegment<byte>(Encoding.ASCII.GetBytes(data)), null);
        }

        public void OnResponseBodyError(Exception error)
        {
            subject.OnError(error);
        }

        public void OnResponseBodyEnd()
        {
            subject.OnEnd();
        }
    }
}
