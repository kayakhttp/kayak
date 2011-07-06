using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Kayak.Cli
{
    class DefaultKayakDelegate : IKayakDelegate
    {
        public void OnStart(IScheduler scheduler, string[] args)
        {
            Console.WriteLine("kayak: scheduler started");
        }

        public void OnException(IScheduler scheduler, Exception e)
        {
            Console.Error.WriteLine("error on scheduler.");
            Console.Error.WriteStacktrace(e);
        }

        public void OnStop(IScheduler scheduler)
        {
            Console.WriteLine("kayak: scheduler stopped");
        }
    }
}
