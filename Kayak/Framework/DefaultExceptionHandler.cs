using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;

namespace Kayak.Framework
{
    class DefaultExceptionHandler : IInvocationExceptionHandler
    {
        public IObservable<Unit> HandleException(IKayakContext context, InvocationInfo info, Exception exception)
        {
            Console.Error.WriteLine("Exception while invoking method " + info.ToString());

            if (exception is TargetInvocationException)
                exception = exception.InnerException;

            Console.Error.WriteException(exception);
            context.Response.SetStatusToInternalServerError();
            return Observable.Empty<Unit>();
        }
    }
}
