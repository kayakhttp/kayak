using System;
using System.IO;
using System.Reflection;

namespace Kayak
{
    public static partial class Extensions
    {
        public static void WriteException(this TextWriter writer, Exception exception)
        {
            int i = 0;
            for (Exception e = exception.InnerException; e != null; e = e.InnerException)
            {
                //if (e is TargetInvocationException || e is AggregateException) continue;

                if (i++ == 0)
                    writer.WriteLine("____________________________________________________________________________");
                else
                    writer.WriteLine("Caused by:");

                writer.WriteLine("[{0}] {1}", e.GetType().Name, e.Message);
                writer.WriteLine(e.StackTrace);
                writer.WriteLine();
            }
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
