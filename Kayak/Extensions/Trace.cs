using System;
using System.Diagnostics;
using System.Text;

namespace Kayak
{
    public static partial class Extensions
    {
        public static void PrintStacktrace(this Exception exception)
        {
#if DEBUG
            var sb = new StringBuilder();

            int i = 0;
            for (Exception e = exception.InnerException; e != null; e = e.InnerException)
            {
                //if (e is TargetInvocationException || e is AggregateException) continue;

                if (i++ == 0)
                    sb.AppendLine("____________________________________________________________________________");
                else
                    sb.AppendLine("Caused by:");

                sb.AppendLine(string.Format("[{0}] {1}", e.GetType().Name, e.Message));
                sb.AppendLine(e.StackTrace);
                sb.AppendLine();
            }

            Debug.WriteLine(sb.ToString());
#endif
        }
    }

    // janky!
    internal static class Trace
    {
        public static void Write(string format, params object[] args)
        {
#if TRACE
            System.Diagnostics.StackTrace stackTrace = new System.Diagnostics.StackTrace();
            System.Diagnostics.StackFrame stackFrame = stackTrace.GetFrame(1);
            System.Reflection.MethodBase methodBase = stackFrame.GetMethod();
            Console.WriteLine("[thread " + System.Threading.Thread.CurrentThread.ManagedThreadId + ", " + methodBase.DeclaringType.Name + "." + methodBase.Name + "] " + string.Format(format, args));
#endif
        }
    }
}
