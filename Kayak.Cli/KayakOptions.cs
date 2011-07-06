using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Mono.Options;
using System.Net;

namespace Kayak.Cli
{
    class GateOptions
    {
        public string GateConfiguration;
        public IPEndPoint ListenEndPoint;
    }

    class EPParser
    {
        public IPEndPoint ParseEP(string epString)
        {
            if (string.IsNullOrEmpty(epString))
                return null;

            // doesn't work with IPv6 right now

            string ipString = null;
            string portString = null;

            if (epString.Contains(':'))
            {
                var colonSplit = epString.Split(':');

                ipString = colonSplit[0];
                portString = colonSplit[1];

                if (colonSplit.Length > 2)
                    goto Error;
            }
            else
            {
                ipString = epString;
                portString = "8000";
            }

            IPAddress ipAddress = null;

            if (!IPAddress.TryParse(ipString, out ipAddress))
                goto Error;

            int port;

            if (!int.TryParse(portString, out port))
                goto Error;

            return new IPEndPoint(ipAddress, port);

        Error:
            throw new Exception("Could not parse IP address from '" + epString + "'");
        }
    }

    class KayakOptions
    {
        public string KayakConfiguration;
        public string[] RemainingArguments;
        public GateOptions GateOptions;
        public bool ShowHelp;

        OptionSet optionSet;

        public KayakOptions(string[] args) : this(args, new EPParser().ParseEP) { }
        public KayakOptions(string[] args, Func<string, IPEndPoint> parseEP)
        {
            bool useGate = false;
            string gateConfiguration = null;
            string listenEndPoint = null;
            List<string> remaining = null;

            optionSet = new OptionSet()
            {
                { "g|gate:", "A Gate configuration string", 
                    v => { useGate = true; gateConfiguration = v; } },
                { "b|bind=", "The endpoint the server should bind to (use in combination with -g/--gate",
                    v => listenEndPoint = v },
                { "h|?|help", "Print help and exit", v => ShowHelp = true },
            };

            remaining = optionSet.Parse(args);

            if (remaining.Count > 0)
            {
                KayakConfiguration = remaining[0];
                remaining.RemoveAt(0);
            }

            RemainingArguments = remaining.ToArray();

            if (useGate)
            {
                GateOptions = new GateOptions();
                GateOptions.GateConfiguration = gateConfiguration;
                GateOptions.ListenEndPoint = parseEP(listenEndPoint);
            }
        }

        public void PrintHelp()
        {
            optionSet.WriteOptionDescriptions(Console.Out);
        }
    }
}
