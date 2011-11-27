using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Kayak.Http;

namespace Kayak.Tests.Http
{
    class RequestDelegate : IHttpRequestDelegate
    {
        User userCode;

        public RequestDelegate(User userCode)
        {
            this.userCode = userCode;
        }

        public void OnRequest(HttpRequestHead head, IDataProducer body, IHttpResponseDelegate response)
        {
            var kayak = new UserKayakAdapter(userCode, body, response);
            userCode.OnRequest(kayak, head);
        }
    }
}
