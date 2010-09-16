using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Kayak.Framework
{
    public interface IInvocationBehavior
    {
        IObservable<InvocationInfo> Bind(IKayakContext context);
        void Invoke(IKayakContext context, IObservable<object> invocation);
    }

    public interface IInvocationArgumentBinder
    {
        void BindArgumentsFromHeaders(IKayakContext context, InvocationInfo info);
        IObservable<Unit> BindArgumentsFromBody(IKayakContext context, InvocationInfo info);
    }

    public interface IInvocationResultHandler
    {
        IObservable<Unit> HandleResult(IKayakContext context, InvocationInfo info, object result);
    }

    public interface IInvocationExceptionHandler
    {
        IObservable<Unit> HandleException(IKayakContext context, InvocationInfo info, Exception exception);
    }
}
