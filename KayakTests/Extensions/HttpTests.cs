using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Kayak;

namespace KayakTests.Extensions
{
    public class HttpTests
    {
        [TestFixture]
        public class GetContentLengthTests
        {
            [Test]
            public void UpperCase()
            {
                var headers = new Dictionary<string, string>();
                headers["Content-Length"] = "3";

                Assert.AreEqual(3, headers.GetContentLength(), "Unexpected content length.");
            }

            [Test]
            public void MixedCase()
            {
                var headers = new Dictionary<string, string>();
                headers["Content-length"] = "3";

                Assert.AreEqual(3, headers.GetContentLength(), "Unexpected content length.");
            }

            [Test]
            public void LowerCase()
            {
                var headers = new Dictionary<string, string>();
                headers["content-length"] = "3";

                Assert.AreEqual(3, headers.GetContentLength(), "Unexpected content length.");
            }

            [Test]
            public void None()
            {
                var headers = new Dictionary<string, string>();

                Assert.AreEqual(-1, headers.GetContentLength(), "Unexpected content length.");
            }
        }

        [TestFixture]
        public class ParseQueryStringTests
        {
            Dictionary<string, string> expected;

            [SetUp]
            public void SetUp()
            {
                expected = new Dictionary<string, string>();
            }

            [Test]
            public void NotAQueryStringAtAll()
            {
                // expect empty dict

                AssertExpectedDict("adskdfkjhfdsk".ParseQueryString());
            }

            [Test]
            public void EndsWithEquals()
            {
                var result = "asdf=".ParseQueryString();

                Assert.IsTrue(result.ContainsKey("asdf"), "Result does not contain expected key");
                Assert.AreEqual(result["asdf"], "", "Unexpected value.");
            }

            [Test]
            public void StartsWithEquals()
            {
                // expect empty dict

                AssertExpectedDict("=asdf".ParseQueryString());
            }

            [Test]
            public void MisplacedEquals()
            {
                var result = "asdf==".ParseQueryString();

                Assert.IsTrue(result.ContainsKey("asdf"), "Result does not contain expected key");
                Assert.AreEqual("", result["asdf"], "Unexpected value.");
            }

            [Test]
            public void OnePair()
            {
                expected["asdf"] = "jkl";

                AssertExpectedDict("asdf=jkl".ParseQueryString());
            }

            [Test]
            public void TwoPair()
            {
                expected["asdf"] = "jkl";
                expected["jkl"] = "asdf";

                AssertExpectedDict("asdf=jkl&jkl=asdf".ParseQueryString());
            }

            [Test]
            public void TrailingAmp()
            {
                expected["asdf"] = "jkl";

                AssertExpectedDict("asdf=jkl&".ParseQueryString());
            }

            public void AssertExpectedDict(IDictionary<string, string> actual)
            {
                Assert.AreEqual(expected.Count, actual.Count, "Unexpected pair count.");
                foreach (var pair in expected)
                {
                    Assert.IsTrue(actual.ContainsKey(pair.Key));
                    Assert.AreEqual(expected[pair.Key], actual[pair.Key], "Unexpected value.");
                }
            }
        }
    }
}
