using System;
using System.Diagnostics;
using Kayak;
using Kayak.Http;
using System.Net;

namespace KayakExamples
{
    class Program
    {
        static void Main(string[] args)
        {
#if DEBUG
            Debug.Listeners.Add(new TextWriterTraceListener(Console.Out));
            Debug.AutoFlush = true;
#endif

            var scheduler = new KayakScheduler();
            scheduler.Post(() =>
            {
                KayakServer.Factory
                    .CreateHttp(new HttpChannel())
                    .Listen(new IPEndPoint(IPAddress.Any, 8080));
            });

            scheduler.Start(new SchedulerDelegate());
        }

        class SchedulerDelegate : ISchedulerDelegate
        {
            public void OnException(IScheduler scheduler, Exception e)
            {
                Debug.WriteLine("Error on scheduler.");
                e.DebugStacktrace();
            }
        }
    }
}
