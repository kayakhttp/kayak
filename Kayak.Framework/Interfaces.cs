using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Kayak.Framework
{
    ///
    /// these are all gone! 
    ///
    
    /// <summary>
    /// Controls the "before and after" behavior of a method invocation in the context of an HTTP transaction.
    /// </summary>
    public interface IInvocationBehavior
    {
        /// <summary>
        /// Returns
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        IObservable<InvocationInfo> Bind();
        void Invoke(IObservable<object> invocation);
    }

    public interface IArgumentBinder
    {
        void BindArgumentsFromHeaders(IKayakContext context, InvocationInfo info);
        IObservable<Unit> BindArgumentsFromBody(IKayakContext context, InvocationInfo info);
    }

    public interface IResultHandler
    {
        IObservable<Unit> HandleResult(IKayakContext context, InvocationInfo info, object result);
    }

    public interface IExceptionHandler
    {
        IObservable<Unit> HandleException(IKayakContext context, InvocationInfo info, Exception exception);
    }
}
