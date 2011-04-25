using System;
using System.Diagnostics;

namespace KayakExamples
{
    class Program
    {
        static void Main(string[] args)
        {
            Debug.Listeners.Add(new TextWriterTraceListener(Console.Out));
            Debug.AutoFlush = true;
            Simple.Run();
        }
    }
}
