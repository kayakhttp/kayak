using System;
using System.Net;
using Gate.Kayak;
using System.Collections.Generic;

namespace Kayak.Cli
{
    class Launcher
    {
        public void LaunchScheduler(KayakOptions options, string searchPath)
        {
            // okay the default delegate kinds sucks because if the user's program doesn't
            // explicitly call stop the thread essentially hangs, becaues the event loop
            // frame never returns, it's just waiting on another task.
            //
            // something of a halting problem.
            // would be better if scheduler just stopped when no more callbacks were queued.
            // 
            // for now, you should use gate if you don't provide a delegate. scheduler and process args
            // are passed in via env. call stop eventually, or be okay with your program never terminating

            var kayakDelegate =
                new KayakDelegateLoader(searchPath).Load(options.KayakConfiguration)
                ?? new DefaultKayakDelegate();

            var scheduler = new KayakScheduler(kayakDelegate);

            scheduler.Post(() => kayakDelegate.OnStart(scheduler, options.RemainingArguments));

            var gateOptions = options.GateOptions;

            if (gateOptions != null)
            {
                var gateConfiguration = gateOptions.GateConfiguration;
                var listenEndPoint = gateOptions.ListenEndPoint;

                scheduler.Post(() =>
                {
                    Console.WriteLine("kayak: running gate configuration '" + gateConfiguration + "'");

                    var context = new Dictionary<string, object>()
                        {
                            { "kayak.ListenEndPoint", listenEndPoint },
                            { "kayak.Arguments", options.RemainingArguments }
                        };
                    var server = KayakServer.Factory.CreateGate(gateConfiguration, scheduler, context);

                    Console.WriteLine("kayak: binding gate app to '" + listenEndPoint + "'");
                    server.Listen(listenEndPoint);
                });
            }
            
            // blocks until Stop is called 
            scheduler.Start();
        }
    }
}
