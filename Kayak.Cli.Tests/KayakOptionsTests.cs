using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Kayak.Cli;
using NUnit.Framework;
using System.Net;

namespace Kayak.Cli.Tests
{
    [TestFixture]
    public class EPParserTests
    {
        public IPEndPoint Parse(string epString)
        {
            return new EPParser().ParseEP(epString);
        }

        [Test]
        public void Parses_IP_address_and_port()
        {
            var result = Parse("0.0.0.0:8999");

            Assert.That(result.Address, Is.EqualTo(IPAddress.Parse("0.0.0.0")));
            Assert.That(result.Port, Is.EqualTo(8999));
        }

        [Test]
        public void Parses_IP_address_and_sets_default_port()
        {
            var result = Parse("0.0.0.0");

            Assert.That(result.Address, Is.EqualTo(IPAddress.Parse("0.0.0.0")));
            Assert.That(result.Port, Is.EqualTo(8000));
        }

        [Test]
        public void Parses_IP_address()
        {
            var result = Parse("1.2.3.4:8999");

            Assert.That(result.Address, Is.EqualTo(IPAddress.Parse("1.2.3.4")));
            Assert.That(result.Port, Is.EqualTo(8999));
        }

        [Test]
        [ExpectedException]
        public void Doesnt_parse_bogus_strings(
            [Values(
                "arse",
                "arse:80",
                "arse:arse",
                "321.0.0.1",
                "321.0.0.1:90",
                "321.0.0.1:arse")]
            string address)
        {
            Parse(address);
        }
    }
    [TestFixture]
    public class KayakOptionsTests
    {
        KayakOptions Parse(params string[] args)
        {
            return new KayakOptionParser().Parse(args);
        }

        [Test]
        public void No_args_gives_null_values()
        {
            var result = Parse();

            Assert.That(result.KayakConfiguration, Is.Null);
            Assert.That(result.RemainingArguments, Is.Empty);
            Assert.That(result.GateOptions, Is.Null);
        }

        [Test]
        public void One_arg_gives_kayak_config()
        {
            var result = Parse("an_argument");
            
            Assert.That(result.KayakConfiguration, Is.EqualTo("an_argument"));
            Assert.That(result.RemainingArguments, Is.Empty);
            Assert.That(result.GateOptions, Is.Null);
        }

        [Test]
        public void One_arg_with_gate_flag_gives_gate_config()
        {
            var result = Parse("--gate=gate_config");

            Assert.That(result.KayakConfiguration, Is.Null);
            Assert.That(result.RemainingArguments, Is.Empty);
            Assert.That(result.GateOptions, Is.Not.Null);
            Assert.That(result.GateOptions.GateConfiguration, Is.EqualTo("gate_config"));
            Assert.That(result.GateOptions.ListenEndPoint, Is.Null);
        }

        public void Two_args_with_gate_flag_gives_gate_config_and_kayak_config()
        {
            var result = Parse("--gate=gate_config", "an_argument");

            Assert.That(result.KayakConfiguration, Is.Null);
            Assert.That(result.RemainingArguments, Is.Empty);
            Assert.That(result.GateOptions, Is.Not.Null);
            Assert.That(result.GateOptions.GateConfiguration, Is.EqualTo("gate_config"));
            Assert.That(result.GateOptions.ListenEndPoint, Is.Null);
        }

        [Test]
        public void Two_args_with_gate_flag_and_bind_flag_gives_gate_config_with_endpoint()
        {
            var result = Parse("--gate=gate_config", "--bind=0.0.0.0");

            Assert.That(result.KayakConfiguration, Is.Null);
            Assert.That(result.RemainingArguments, Is.Empty);
            Assert.That(result.GateOptions, Is.Not.Null);
            Assert.That(result.GateOptions.GateConfiguration, Is.EqualTo("gate_config"));
            Assert.That(result.GateOptions.ListenEndPoint, Is.EqualTo(new IPEndPoint(IPAddress.Parse("0.0.0.0"), 8000)));
        }

        [Test]
        public void Two_args_with_gate_flag_and_kayak_config_flag_gives_kayak_config_and_gate_config()
        {
            var result = Parse("--gate=gate_config", "kayak_config");

            Assert.That(result.KayakConfiguration, Is.EqualTo("kayak_config"));
            Assert.That(result.RemainingArguments, Is.Empty);
            Assert.That(result.GateOptions, Is.Not.Null);
            Assert.That(result.GateOptions.GateConfiguration, Is.EqualTo("gate_config"));
            Assert.That(result.GateOptions.ListenEndPoint, Is.Null);
        }

        [Test]
        public void Two_args_with_bind_flag_and_kayak_config_gives_kayak_config_and_no_gate_config()
        {
            // i.e., bind is ignored
            var result = Parse("--bind=bind_ep", "kayak_config");

            Assert.That(result.KayakConfiguration, Is.EqualTo("kayak_config"));
            Assert.That(result.RemainingArguments, Is.Empty);
            Assert.That(result.GateOptions, Is.Null);
        }

        [Test]
        public void Three_args_with_gate_flag_and_bind_flag_and_kayak_config_gives_kayak_config_and_populated_gate_config()
        {
            var result = Parse("--gate=gate_config", "--bind=0.0.0.0", "kayak_config");

            Assert.That(result.KayakConfiguration, Is.EqualTo("kayak_config"));
            Assert.That(result.RemainingArguments, Is.Empty);
            Assert.That(result.GateOptions, Is.Not.Null);
            Assert.That(result.GateOptions.GateConfiguration, Is.EqualTo("gate_config"));
            Assert.That(result.GateOptions.ListenEndPoint, Is.EqualTo(new IPEndPoint(IPAddress.Parse("0.0.0.0"), 8000)));
        }

        [Test]
        public void Three_args_with_bind_flag_and_gate_flag_and_kayak_config_gives_kayak_config_and_populated_gate_config()
        {
            var result = Parse("--bind=0.0.0.0", "--gate=gate_config", "kayak_config");

            Assert.That(result.KayakConfiguration, Is.EqualTo("kayak_config"));
            Assert.That(result.RemainingArguments, Is.Empty);
            Assert.That(result.GateOptions, Is.Not.Null);
            Assert.That(result.GateOptions.GateConfiguration, Is.EqualTo("gate_config"));
            Assert.That(result.GateOptions.ListenEndPoint, Is.EqualTo(new IPEndPoint(IPAddress.Parse("0.0.0.0"), 8000)));
        }

        [Test]
        public void Three_args_with_bind_flag_and_empty_gate_flag_and_kayak_config_gives_kayak_config_and_populated_gate_config()
        {
            var result = Parse("--bind=0.0.0.0", "--gate", "kayak_config");

            Assert.That(result.KayakConfiguration, Is.EqualTo("kayak_config"));
            Assert.That(result.RemainingArguments, Is.Empty);
            Assert.That(result.GateOptions, Is.Not.Null);
            Assert.That(result.GateOptions.GateConfiguration, Is.Null);
            Assert.That(result.GateOptions.ListenEndPoint, Is.EqualTo(new IPEndPoint(IPAddress.Parse("0.0.0.0"), 8000)));
        }

        // would be nice to pairwise this will all other tests
        [Test]
        public void Parses_remaining_args()
        {
            var result = Parse("--bind=0.0.0.0", "--gate=gate_config", "kayak_config", "[1]", "[2]");

            Assert.That(result, Is.Not.Null);
            Assert.That(result.KayakConfiguration, Is.EqualTo("kayak_config"));
            Assert.That(result.GateOptions, Is.Not.Null);
            Assert.That(result.GateOptions.GateConfiguration, Is.EqualTo("gate_config"));
            Assert.That(result.GateOptions.ListenEndPoint, Is.EqualTo(new IPEndPoint(IPAddress.Parse("0.0.0.0"), 8000)));
            Assert.That(result.RemainingArguments, Is.Not.Null);
            Assert.That(result.RemainingArguments.Length, Is.EqualTo(2));
            Assert.That(result.RemainingArguments[0], Is.EqualTo("[1]"));
            Assert.That(result.RemainingArguments[1], Is.EqualTo("[2]"));
        }
    }
}
