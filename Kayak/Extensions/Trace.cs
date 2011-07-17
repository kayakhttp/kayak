using System;
using System.Diagnostics;
using System.Text;
using System.IO;

namespace Kayak
{
    public static partial class Extensions
    {
        public static void DebugStacktrace(this Exception exception)
        {
#if DEBUG
            Debug.WriteLine(GetStackTrace(exception));
#endif
        }

        public static void WriteStacktrace(this TextWriter writer, Exception exception)
        {
            writer.WriteLine(GetStackTrace(exception));
        }

        public static string GetStackTrace(Exception exception)
        {
            var sb = new StringBuilder();

            int i = 0;
            for (Exception e = exception; e != null; e = e.InnerException)
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

            return sb.ToString();
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
