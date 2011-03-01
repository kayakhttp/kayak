using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Kayak;
using NUnit.Framework;
using Kayak.Http;

namespace KayakTests
{

    class TestRequest
    {
        public string Method, Uri;
        public Version Version;
        public IDictionary<string, string> Headers;
        public bool KeepAlive;
        public byte[] Body;

        public static TestRequest OneOhGetKeepAlive = new TestRequest()
        {
            Method = "GET",
            Uri = "/",
            Version = new Version(1, 0),
            Headers = new Dictionary<string, string>()
                {
                    { "Foo", "Bar" }
                },
            KeepAlive = true
        };

        public static TestRequest OneOhGet = new TestRequest()
        {
            Method = "GET",
            Uri = "/",
            Version = new Version(1, 0),
            Headers = new Dictionary<string, string>()
                {
                    { "Foo", "Bar" }
                },
            KeepAlive = false
        };

        public static TestRequest OneOhPost = new TestRequest()
        {
            Method = "POST",
            Uri = "/",
            Version = new Version(1, 0),
            Headers = new Dictionary<string, string>()
                {
                    { "Foo", "Bar" }
                },
            KeepAlive = false,
            Body = Encoding.UTF8.GetBytes("hello world")
        };

        public static TestRequest OneOhPostKeepAlive = new TestRequest()
        {
            Method = "POST",
            Uri = "/",
            Version = new Version(1, 0),
            Headers = new Dictionary<string, string>()
                {
                    { "Foo", "Bar" }
                },
            KeepAlive = true,
            Body = Encoding.UTF8.GetBytes("hello world")
        };

        public static void AssertAreEqual(TestRequest expected, IRequest actual)
        {
            Assert.AreEqual(expected.Method, actual.Method, "Unexpected method.");
            Assert.AreEqual(expected.Uri, actual.Uri, "Unexpected URI.");
            Assert.AreEqual(expected.Version, actual.Version, "Unexpected version.");
            AssertHeaders(expected.Headers, actual.Headers);
        }

        static void AssertHeaders(IDictionary<string, string> expected, IDictionary<string, string> actual)
        {
            foreach (var pair in expected)
            {
                Assert.IsTrue(actual.ContainsKey(pair.Key), "Actual headers did not contain key '" + pair.Key + "'");
                Assert.AreEqual(pair.Value, actual[pair.Key], "Actual headers had wrong value for key '" + pair.Key + "'");
            }

            foreach (var pair in actual)
            {
                Assert.IsTrue(expected.ContainsKey(pair.Key), "Unexpected header named '" + pair.Key + "'");
            }
        }
    }
}
