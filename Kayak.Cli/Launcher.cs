using System;
using System.Net;
using Gate.Kayak;
using System.Collections.Generic;
using Gate;

namespace Kayak.Cli
{
    class Launcher
    {
        public void LaunchScheduler(KayakOptions options)
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
                new KayakDelegateLoader().Load(options.KayakConfiguration)
                ?? new DefaultKayakDelegate();

            var scheduler = KayakScheduler.Factory.Create(kayakDelegate);

            scheduler.Post(() => kayakDelegate.OnStart(scheduler, options.RemainingArguments));

            var gateOptions = options.GateOptions;

            if (gateOptions != null)
            {
                var gateConfiguration = gateOptions.GateConfiguration;
                var listenEndPoint = gateOptions.ListenEndPoint ?? new IPEndPoint(IPAddress.Any, 8000);

                var context = new Dictionary<string, object>()
                        {
                            { "kayak.ListenEndPoint", listenEndPoint },
                            { "kayak.Arguments", options.RemainingArguments }
                        };

                var config = DefaultConfigurationLoader.LoadConfiguration(gateConfiguration);

                if (config == null)
                    throw new Exception("Could not load Gate configuration from configuration string '" + gateConfiguration + "'");

                var app = AppBuilder.BuildConfiguration(config);

                var server = KayakServer.Factory.CreateGate(app, scheduler, context);

                scheduler.Post(() =>
                {
                    Console.WriteLine("kayak: binding gate app to '" + listenEndPoint + "'");
                    server.Listen(listenEndPoint);
                });
            }
            
            // blocks until Stop is called 
            scheduler.Start();
        }
    }
}
