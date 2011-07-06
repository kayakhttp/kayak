using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using Mono.Options;

namespace Kayak.Cli
{
    class Program
    {
        static int Main(string[] args)
        {
            Debug.Listeners.Add(new TextWriterTraceListener(Console.Out));
            Debug.AutoFlush = true;

            KayakOptions options = null;

            try
            {
                options = new KayakOptions(args);
            }
            catch (OptionException e)
            {
                Console.Write("kayak: ");
                Console.WriteLine(e.Message);
                Console.WriteLine("Type `kayak --help` for more information.");
            }

            if (options.ShowHelp)
            {
                options.PrintHelp();
                return 0;
            }

            if (options == null)
                return 1;

            try
            {
                Console.Write("kayak: launching scheduler");
                if (!string.IsNullOrEmpty(options.KayakConfiguration))
                    Console.WriteLine(" with delegate '" + options.KayakConfiguration + "'");
                else
                    Console.WriteLine();

                AppDomain.CurrentDomain.AssemblyResolve += new ResolveEventHandler((object sender, ResolveEventArgs a) =>
                {
                    return Assembly.LoadFrom(Path.Combine(
                        Directory.GetCurrentDirectory(),
                        a.Name.Substring(0, a.Name.IndexOf(',')) + ".dll"));
                });

                new Launcher().LaunchScheduler(options);
            }
            catch (Exception e)
            {
                Console.Error.WriteLine("kayak: failed to launch scheduler");
                Console.Error.WriteLine(e.Message);
                Console.Error.WriteStacktrace(e);
                return -1;
            }

            return 0;
        }
    }
}
