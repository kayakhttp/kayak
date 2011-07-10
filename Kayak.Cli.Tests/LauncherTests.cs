using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Kayak.Cli.Tests;
using Gate;
using System.Diagnostics;

namespace Kayak.Cli.Tests
{
    [TestFixture]
    class LauncherTests
    {
        public void Launch(KayakOptions options)
        {
            // might be nice to:
            //Process.Start(new ProcessStartInfo()
            //{
            //    WorkingDirectory = AppDomain.CurrentDomain.SetupInformation.ApplicationBase,
            //    Arguments = ...
            //});
            new Launcher().LaunchScheduler(options, AppDomain.CurrentDomain.SetupInformation.ApplicationBase);
        }

        [SetUp]
        public void SetUp()
        {
            KayakDelegate.Reset();
            Startup.Reset();
        }

        [Test]
        public void Locates_and_calls_KayakDelegate_and_Gate_configuration_defaults()
        {
            Startup.StopOnConfigure = true;
            string[] args = new[] { "asdf" };
            Launch(new KayakOptions()
            {
                GateOptions = new GateOptions(),
                RemainingArguments = args
            });

            Assert.That(KayakDelegate.OnStartCalls, Is.EqualTo(1));
            Assert.That(KayakDelegate.OnStartArgs, Is.SameAs(args));
            Assert.That(Startup.ConfigurationCalls, Is.EqualTo(1));
        }

        [Test]
        public void Locates_and_calls_KayakDelegate()
        {
            KayakDelegate.StopOnStart = true;
            string[] args = new[] { "asdf" };
            Launch(new KayakOptions()
            {
                KayakConfiguration = "Kayak.Cli.Tests.KayakDelegate",
                RemainingArguments = args
            });

            Assert.That(KayakDelegate.OnStartCalls, Is.EqualTo(1));
            Assert.That(KayakDelegate.OnStartArgs, Is.SameAs(args));
            Assert.That(Startup.ConfigurationCalls, Is.EqualTo(0));
        }

        [Test]
        public void Locates_and_calls_KayakDelegate_and_Gate_configuration()
        {
            Startup.StopOnConfigure = true;
            string[] args = new[] { "asdf" };
            Launch(new KayakOptions()
            {
                KayakConfiguration = "Kayak.Cli.Tests.KayakDelegate",
                GateOptions = new GateOptions(),
                RemainingArguments = args
            });

            Assert.That(KayakDelegate.OnStartCalls, Is.EqualTo(1));
            Assert.That(KayakDelegate.OnStartArgs, Is.SameAs(args));
            Assert.That(Startup.ConfigurationCalls, Is.EqualTo(1));
        }
    }

    public class Startup
    {
        public static void Reset()
        {
            ConfigurationCalls = 0;
            StopOnConfigure = false;
            Scheduler = null;
        }

        public static int ConfigurationCalls;
        public static bool StopOnConfigure;
        public static IScheduler Scheduler;

        public void Configuration(IAppBuilder b)
        {
            ConfigurationCalls++;
            if (StopOnConfigure)
                Scheduler.Stop();
        }
    }

    public class KayakDelegate : IKayakDelegate
    {
        public static void Reset()
        {
            StopOnStart = false;
            OnStartCalls = 0;
            OnStartArgs = null;
        }

        public static bool StopOnStart;
        public static int OnStartCalls;
        public static string[] OnStartArgs;

        public void OnStart(IScheduler scheduler, string[] args)
        {
            OnStartCalls++;
            OnStartArgs = args;
            Startup.Scheduler = scheduler;
            if (StopOnStart)
                scheduler.Stop();
        }

        public void OnException(IScheduler scheduler, Exception e)
        {

        }

        public void OnStop(IScheduler scheduler)
        {
        }
    }
}