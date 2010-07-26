using System;
using System.Disposables;
using System.IO;
using System.Text;
using System.Web;
using System.Collections;
using System.Diagnostics;
using System.Reflection;
using System.Threading;

namespace Kayak
{
    static class Trace
    {
        public static void Write(string format, params object[] args)
        {
            //StackTrace stackTrace = new StackTrace();
            //StackFrame stackFrame = stackTrace.GetFrame(1);
            //MethodBase methodBase = stackFrame.GetMethod();
            //Console.WriteLine("[thread " + Thread.CurrentThread.ManagedThreadId + ", " + methodBase.DeclaringType.Name + "." + methodBase.Name + "] " + string.Format(format, args));
        }
    }

    public static partial class Extensions
    {
        public static void WriteException(this TextWriter writer, Exception exception)
        {
            writer.WriteLine("____________________________________________________________________________");

            writer.WriteLine("[{0}] {1}", exception.GetType().Name, exception.Message);
            writer.WriteLine(exception.StackTrace);
            writer.WriteLine();

            for (Exception e = exception.InnerException; e != null; e = e.InnerException)
            {
                writer.WriteLine("Caused by:\r\n[{0}] {1}", e.GetType().Name, e.Message);
                writer.WriteLine(e.StackTrace);
                writer.WriteLine();
            }
        }

        public static IObservable<int> ReadAsync(this Stream s, byte[] buffer, int offset, int count)
        {
            return new AsyncOperation<int>((cb, st) => s.BeginRead(buffer, offset, count, cb, st), s.EndRead);
            //return Observable.FromAsyncPattern<byte[], int, int, int>(s.BeginRead, s.EndRead)(buffer, offset, count).Repeat(1);
        }

        public static IObservable<Unit> WriteAsync(this Stream s, byte[] buffer, int offset, int count)
        {
            return new AsyncOperation<Unit>((c, st) => s.BeginWrite(buffer, offset, count, c, st), iasr => { s.EndWrite(iasr); return new Unit(); });
            //return Observable.FromAsyncPattern<byte[], int, int>(s.BeginWrite, s.EndWrite)(buffer, offset, count).Repeat(1);
        }

        #region Common Statuses
        // might set an internal flag on some of these so that we can send some default content (i.e., 404 page)

        public static void SetStatusToOK(this IKayakServerResponse response)
        {
            response.StatusCode = 200;
            response.ReasonPhrase = "OK";
        }

        public static void SetStatusToCreated(this IKayakServerResponse response)
        {
            response.StatusCode = 201;
            response.ReasonPhrase = "Created";
        }

        public static void SetStatusToFound(this IKayakServerResponse response)
        {
            response.StatusCode = 302;
            response.ReasonPhrase = "Found";
        }

        public static void SetStatusToNotModified(this IKayakServerResponse response)
        {
            response.StatusCode = 304;
            response.ReasonPhrase = "Not Modified";
        }

        public static void SetStatusToBadRequest(this IKayakServerResponse response)
        {
            response.StatusCode = 400;
            response.ReasonPhrase = "Bad Request";
        }

        public static void SetStatusToForbidden(this IKayakServerResponse response)
        {
            response.StatusCode = 403;
            response.ReasonPhrase = "Forbidden";
        }

        public static void SetStatusToNotFound(this IKayakServerResponse response)
        {
            response.StatusCode = 404;
            response.ReasonPhrase = "Not Found";
        }

        public static void SetStatusToConflict(this IKayakServerResponse response)
        {
            response.StatusCode = 409;
            response.ReasonPhrase = "Conflict";
        }

        public static void SetStatusToInternalServerError(this IKayakServerResponse response)
        {
            response.StatusCode = 503;
            response.ReasonPhrase = "Internal Server Error";
        }

        #endregion

        public static void Redirect(this IKayakServerResponse response, string url)
        {
            // clear response?
            response.SetStatusToFound();
            response.Headers["Location"] = url;
        }

        #region Query string extensions


        const char EqualsChar = '=';
        const char AmpersandChar = '&';

        public static NameValueDictionary DecodeQueryString(this string encodedString)
        {
            return DecodeQueryString(encodedString, 0, encodedString.Length);
        }

        public static NameValueDictionary DecodeQueryString(this string encodedString,
            int charIndex, int charCount)
        {
            var result = new NameValueDictionary();
            var name = new StringBuilder();
            var value = new StringBuilder();
            var hasValue = false;

            for (int i = charIndex; i < charIndex + charCount; i++)
            {
                char c = encodedString[i];
                switch (c)
                {
                    case EqualsChar:
                        hasValue = true;
                        break;
                    case AmpersandChar:
                        // end of pair
                        AddNameValuePair(result, name, value, hasValue);

                        // reset
                        name.Length = value.Length = 0;
                        hasValue = false;
                        break;
                    default:
                        if (!hasValue) name.Append(c); else value.Append(c);
                        break;
                }
            }

            // last pair
            AddNameValuePair(result, name, value, hasValue);

            return result;
        }

        static void AddNameValuePair(NameValueDictionary dict, StringBuilder name, StringBuilder value, bool hasValue)
        {
            if (name.Length > 0)
                dict.Add(HttpUtility.UrlDecode(name.ToString()), hasValue ? HttpUtility.UrlDecode(value.ToString()) : null);
        }

        public static string ToQueryString(this NameValueDictionary dict)
        {
            var sb = new StringBuilder();

            foreach (NameValuePair pair in dict)
            {
                foreach (string value in pair.Values)
                {
                    if (sb.Length > 0)
                        sb.Append(AmpersandChar);

                    sb.Append(Uri.EscapeDataString(pair.Name))
                        .Append(EqualsChar)
                        .Append(Uri.EscapeDataString(value));
                }
            }

            return sb.ToString();
        }

        public static string ToQueryString(this IDictionary dict)
        {
            var sb = new StringBuilder();

            foreach (var key in dict.Keys)
            {
                if (sb.Length > 0)
                    sb.Append(AmpersandChar);

                sb.Append(Uri.EscapeDataString(key.ToString()))
                    .Append(EqualsChar)
                    .Append(Uri.EscapeDataString(dict[key].ToString()));
            }

            return sb.ToString();
        }

        #endregion

    }

    // want to get rid of this class.
    class AsyncOperation<T> : IObservable<T>
    {
        Func<AsyncCallback, object, IAsyncResult> begin;
        Func<IAsyncResult, T> end;

        public AsyncOperation(Func<AsyncCallback, object, IAsyncResult> begin, Func<IAsyncResult, T> end)
        {
            this.begin = begin;
            this.end = end;
        }

        #region IObservable<T> Members

        public IDisposable Subscribe(IObserver<T> observer)
        {
            IAsyncResult asyncResult = null;

            AsyncCallback callback = (iasr) =>
            {
                //Console.WriteLine("Got async callback");
                T value = default(T);
                try
                {
                    value = end(iasr);
                }
                catch (Exception e)
                {
                    observer.OnError(e);
                    return;
                }
                observer.OnNext(value);
                observer.OnCompleted();
            };

            asyncResult = begin(callback, null);

            return Disposable.Empty;
        }

        #endregion

        //~AsyncOperation()
        //{
        //    Console.WriteLine("AsyncOperation deallocd");
        //}
    }
}
