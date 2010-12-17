using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Owin;
using System.Threading.Tasks;

namespace Kayak
{
    public class ErrorHandlingMiddleware : IApplication
    {
        IApplication wrap;
        IResponse fiveOhThree;
        public ErrorHandlingMiddleware(IApplication wrap, IResponse fiveOhThree)
        {
            this.wrap = wrap;
            this.fiveOhThree = fiveOhThree;
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
            var invoke = wrap.InvokeAsync(request);

            yield return invoke;

            if (invoke.IsFaulted)
            {
                yield return fiveOhThree;
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
                yield return fiveOhThree;
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
                yield return fiveOhThree;
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
                        yield return fiveOhThree;
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

                while (rest.MoveNext())
                {
                    yield return rest.Current;
                }
            }
        }
    }
}
