using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Owin;

namespace Kayak
{
    public interface IHttpErrorHandler
    {
        // called if an exception occurs before the response has begun.
        // this should probably be async
        IResponse GetExceptionResponse(Exception exception);

        // called if an exception occurs after the response has begun
        Task WriteExceptionText(Exception exception, ISocket socket);
    }

    public class HttpErrorHandler : IHttpErrorHandler
    {
        public IResponse GetExceptionResponse(Exception e)
        {
            var sw = new StringWriter();
            sw.WriteException(e);
            Console.WriteLine("Exception while processing request.");
            Console.Out.WriteException(e);
            return new KayakResponse("503 Internal Server Error", new Dictionary<string, IEnumerable<string>>(), sw.ToString());
        }

        public Task WriteExceptionText(Exception e, ISocket socket)
        {
            var sw = new StringWriter();
            sw.WriteException(e);
            Console.WriteLine("Exception while processing request.");
            Console.Out.WriteException(e);
            return socket.WriteObject(sw.ToString());
        }
    }
}
