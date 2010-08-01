using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Kayak.Framework
{
    class DefaultExceptionHandler : IInvocationExceptionHandler
    {
        public IObservable<Unit> HandleException(IKayakContext context, InvocationInfo info, Exception exception)
        {
            context.Response.SetStatusToInternalServerError();
            return Observable.Empty<Unit>();
        }
    }
}
