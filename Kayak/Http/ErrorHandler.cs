using System;
using System.Collections.Generic;
using System.IO;
using Owin;

namespace Kayak
{
    public interface IHttpErrorHandler
    {
        // called if an exception occurs before the response has begun.
        // this should probably be async
        IResponse GetExceptionResponse(Exception exception);

        // called if an exception occurs after the response has begun
        IObservable<Unit> WriteExceptionText(Exception exception, ISocket socket);
    }

    public class HttpErrorHandler : IHttpErrorHandler
    {
        public IResponse GetExceptionResponse(Exception e)
        {
            var sw = new StringWriter();
            sw.WriteException(e);
            return new KayakResponse("503 Internal Server Error", new Dictionary<string, IEnumerable<string>>(), sw);
        }

        public IObservable<Unit> WriteExceptionText(Exception exception, ISocket socket)
        {
            throw new NotImplementedException();
        }
    }
}
