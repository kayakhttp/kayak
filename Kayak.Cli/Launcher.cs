using System;
using System.Net;
using Gate.Kayak;

namespace Kayak.Cli
{
    class Launcher
    {
        public void LaunchScheduler(KayakOptions options)
        {
            var kayakDelegate =
                new KayakDelegateLoader().Load(options.KayakConfiguration)
                ?? new DefaultKayakDelegate();

            var scheduler = new KayakScheduler(kayakDelegate);

            var gateOptions = options.GateOptions;

            if (gateOptions != null)
            {
                CreateGateServer(KayakServer.Factory, scheduler,
                    options.GateOptions.ListenEndPoint, options.GateOptions.GateConfiguration);
            }

            LaunchScheduler(scheduler, kayakDelegate, options.RemainingArguments);
        }

        public void CreateGateServer(IServerFactory serverFactory, IScheduler scheduler, IPEndPoint listenEndPoint, string gateConfiguration)
        {
            Console.WriteLine("kayak: running gate configuration '" + gateConfiguration + "'");
            var server = serverFactory.CreateGate(gateConfiguration, scheduler);

            scheduler.Post(() =>
            {
                Console.WriteLine("kayak: binding gate app to '" + listenEndPoint + "'");
                server.Listen(listenEndPoint);
            });
        }

        public void LaunchScheduler(IScheduler scheduler, IKayakDelegate kayakDelegate, string[] arguments)
        {
            scheduler.Post(() => kayakDelegate.OnStart(scheduler, arguments));
            scheduler.Start();
        }
    }
}
