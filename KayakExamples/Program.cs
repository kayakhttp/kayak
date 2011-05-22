using System;
using System.Diagnostics;
using System.Net;
using Kayak;
using Kayak.Http;

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

            var scheduler = new KayakScheduler(new SchedulerDelegate());
            scheduler.Post(() =>
            {
                KayakServer.Factory
                    .CreateHttp(new Channel())
                    .Listen(new IPEndPoint(IPAddress.Any, 8080));
            });

            scheduler.Start();
        }

        class SchedulerDelegate : ISchedulerDelegate
        {
            public void OnException(IScheduler scheduler, Exception e)
            {
                Debug.WriteLine("Error on scheduler.");
                e.DebugStacktrace();
            }

            public void OnStop(IScheduler scheduler)
            {

            }
        }
    }
}
