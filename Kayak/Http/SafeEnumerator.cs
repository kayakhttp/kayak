using System;
using System.Collections.Generic;
using System.Linq;
using Owin;

namespace Kayak.Http
{
    static class SafeResponseEnumerator
    {
        // if enumerating the first item throws exception, this method will throw.
        // any subsequent errors during enumeration will cause the sequence to end
        // with an observable which will write a textual description of the error
        // if an error handler is provided (if no error handler, sequence ends on error)
        public static IEnumerator<object> Create(IResponse response, ISocket socket, IHttpErrorHandler errorHandler)
        {
            var en = response.GetBody().GetEnumerator();

            try
            {
                if (en.MoveNext())
                    return new object[] { en.Current }.Concat(DoSafeEnum(en, socket, errorHandler)).GetEnumerator();
                else
                    return Enumerable.Empty<object>().GetEnumerator();
            }
            catch
            {
                // possible to get object disposed if the using block in DoSafeEnum somehow disposes `en` first?
                en.Dispose();
                throw;
            }
        }

        static IEnumerable<object> DoSafeEnum(IEnumerator<object> en, ISocket socket, IHttpErrorHandler errorHandler)
        {
            using (en)
            {
                bool continues = false;
                do
                {
                    object current = null;
                    Exception ex = null;
                    try
                    {
                        continues = en.MoveNext();

                        if (continues)
                            current = en.Current;
                    }
                    catch (Exception e)
                    {
                        ex = e;
                    }

                    if (ex != null)
                    {
                        yield return errorHandler.WriteExceptionText(ex, socket);
                        yield break;
                    }

                    if (!continues)
                        yield break;

                    yield return current;
                }
                while (continues);
            }
        }
    }
}
