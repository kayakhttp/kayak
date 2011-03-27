using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Kayak;
using Kayak.Http;

namespace Kayak.Owin
{
    public class OwinServerDelegate : IHttpServerDelegate
    {
        public void OnRequest(IRequest request, IResponse response)
        {
            throw new NotImplementedException();
        }
    }
}
