using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Owin;
using System.IO;
using System.Text;

namespace Kayak
{
    public interface IErrorHandler
    {
        // called if an exception occurs before the response has begun.
        IResponse GetExceptionResponse(IApplication threw, Exception exception);

        // called if an exception occurs after the response has begun.
        string GetExceptionText(IApplication threw, Exception exception);
    }

    public class ErrorHandler : IErrorHandler
    {
        public IResponse GetExceptionResponse(IApplication threw, Exception e)
        {
            var sw = new StringWriter();
            sw.WriteException(e);
            Console.WriteLine("Exception while processing request.");
            Console.Out.WriteException(e);
            return new KayakResponse("503 Internal Server Error",
                new Dictionary<string, IEnumerable<string>>()
                {
                    { "Content-Type", new string[] { "text/html" } }
                },
                "<pre>" + sw.ToString() + "</pre>");
        }

        public string GetExceptionText(IApplication threw, Exception e)
        {
            var sw = new StringWriter();
            sw.WriteException(e);
            Console.WriteLine("Exception while processing request.");
            Console.Out.WriteException(e);
            return sw.ToString();
        }
    }

    public class ErrorHandlingMiddleware : IApplication
    {
        IApplication wrapped;
        IErrorHandler errorHandler;

        public ErrorHandlingMiddleware(IApplication wrapped, IErrorHandler errorHandler)
        {
            this.wrapped = wrapped;
            this.errorHandler = errorHandler;
        }

        public IAsyncResult BeginInvoke(IRequest request, AsyncCallback callback, object state)
        {
            var tcs = new TaskCompletionSource<IResponse>();

            SafeInvoke(request).AsCoroutine<IResponse>().ContinueWith(t =>
                {
                    if (t.IsFaulted)
                        tcs.SetException(t.Exception);
                    else
                        tcs.SetResult(t.Result);

                    callback(tcs.Task);
                });

            return tcs.Task;
        }

        public IResponse EndInvoke(IAsyncResult result)
        {
            var task = result as Task<IResponse>;

            if (task == null)
                throw new ArgumentException("Invalid IAsyncResult");

            if (task.IsFaulted)
                throw task.Exception;

            return task.Result;
        }

        IEnumerable<object> SafeInvoke(IRequest request)
        {
            var invoke = wrapped.InvokeAsync(request);

            yield return invoke;

            if (invoke.IsFaulted)
            {
                yield return errorHandler.GetExceptionResponse(wrapped, invoke.Exception);
                yield break;
            }

            IResponse response = invoke.Result;
            Exception ex = null;
            IEnumerator<object> body = null;
 
            try
            {
                body = response.GetBody().GetEnumerator();
            }
            catch (Exception e)
            {
                ex = e;
            }

            if (ex != null)
            {
                yield return errorHandler.GetExceptionResponse(wrapped, invoke.Exception);
                yield break;
            }

            bool continues = false;
            object firstObject = null;

            try
            {
                continues = body.MoveNext();

                if (continues)
                    firstObject = body.Current;
            }
            catch (Exception e)
            {
                ex = e;
            }

            if (ex != null)
            {
                body.Dispose();
                yield return errorHandler.GetExceptionResponse(wrapped, invoke.Exception);
                yield break;
            }

            IEnumerable<object> newBody = null;

            if (continues)
            {
                if (firstObject is Task)
                {
                    var task = (firstObject as Task).AsTaskWithValue();

                    yield return task;

                    if (task.IsFaulted)
                    {
                        yield return errorHandler.GetExceptionResponse(wrapped, invoke.Exception);
                        yield break;
                    }

                    firstObject = task.Result;
                }

                newBody = EnumerateBody(firstObject, body);
            }

            yield return new KayakResponse(response.Status, response.Headers, newBody);
        }

        IEnumerable<object> EnumerateBody(object first, IEnumerator<object> rest)
        {
            using (rest)
            {
                yield return first;

                while (true)
                {
                    var continues = false;
                    object current = null;
                    Exception exception = null;

                    try
                    {
                        continues = rest.MoveNext();

                        if (continues)
                            current = rest.Current;
                    }
                    catch (Exception e)
                    {
                        exception = e;
                    }

                    if (exception != null)
                    {
                        yield return Encoding.ASCII.GetBytes(errorHandler.GetExceptionText(wrapped, exception));
                        break;
                    }

                    if (continues)
                        yield return rest.Current;
                    else
                        break;
                }
            }
        }
    }
}
