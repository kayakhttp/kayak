using System;
using System.IO;

namespace Kayak
{
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
    }

    // janky!
    static class Trace
    {
        public static void Write(string format, params object[] args)
        {
            //System.Diagnostics.StackTrace stackTrace = new System.Diagnostics.StackTrace();
            //System.Diagnostics.StackFrame stackFrame = stackTrace.GetFrame(1);
            //System.Reflection.MethodBase methodBase = stackFrame.GetMethod();
            //Console.WriteLine("[thread " + System.Threading.Thread.CurrentThread.ManagedThreadId + ", " + methodBase.DeclaringType.Name + "." + methodBase.Name + "] " + string.Format(format, args));
        }
    }
}
