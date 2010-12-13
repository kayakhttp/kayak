using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Kayak;
using Moq;
using System.IO;

namespace KayakTests.Extensions
{
    public class HttpTests
    {
        [TestFixture]
        public class GetContentLengthTests
        {
            Dictionary<string, IEnumerable<string>> Header(string name, params string[] values)
            {
                var headers = new Dictionary<string, IEnumerable<string>>();
                headers[name] = values;
                return headers;
            }

            [Test]
            public void CaseInsensitive()
            {
                Assert.AreEqual(3, Header("Content-Length", "3").GetContentLength());
                Assert.AreEqual(3, Header("Content-length", "3").GetContentLength());
                Assert.AreEqual(3, Header("content-Length", "3").GetContentLength());
                Assert.AreEqual(3, Header("content-length", "3").GetContentLength());
                Assert.AreEqual(3, Header("ConTenT-LeNGth", "3").GetContentLength());
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

        [TestFixture]
        public class ReadRequestLineTests
        {
            Mock<TextReader> mockReader;

            [SetUp]
            public void SetUp()
            {
                mockReader = new Mock<TextReader>();
            }

            [Test]
            [ExpectedException(
                ExpectedException = typeof(Exception), 
                ExpectedMessage = "Could not parse request status.")]
            public void EmptyStatusLine()
            {
                var reader = new StringReader("");

                reader.ReadRequestLine();
            }

            [Test]
            [ExpectedException(
                ExpectedException = typeof(Exception),
                ExpectedMessage = "Expected 2 or 3 tokens in request line.")]
            public void MalformedStatusLine()
            {
                var reader = new StringReader("asdasdf");

                reader.ReadRequestLine();
            }

            [Test]
            [ExpectedException(
                ExpectedException = typeof(Exception),
                ExpectedMessage = "Expected 2 or 3 tokens in request line.")]
            public void MalformedStatusLine3()
            {
                var reader = new StringReader("asdasd  fds sd dsfasdf");

                reader.ReadRequestLine();
            }

            [Test]
            [ExpectedException(
                ExpectedException = typeof(Exception),
                ExpectedMessage = "Expected 2 or 3 tokens in request line.")]
            public void MalformedStatusLine4()
            {
                var reader = new StringReader("asd asd d fs  aaaa    ");

                reader.ReadRequestLine();
            }

            [Test]
            public void DefaultHttpVersion()
            {
                var reader = new StringReader("asdas df");

                var requestLine = reader.ReadRequestLine();

                Assert.AreEqual("HTTP/1.0", requestLine.HttpVersion);
            }

            [Test]
            public void GivenHttpVersion()
            {
                var reader = new StringReader("asdas df asdf");

                var requestLine = reader.ReadRequestLine();

                Assert.AreEqual("asdf", requestLine.HttpVersion);
            }
        }
    }
}
