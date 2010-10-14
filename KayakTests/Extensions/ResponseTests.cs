using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit;
using Kayak;
using NUnit.Framework;

namespace KayakTests.Extensions
{
    public class ResponseTests
    {
        [TestFixture]
        public class ToResponseTests
        {
            [Test]
            [ExpectedException(typeof(ArgumentException), ExpectedMessage = "response status is not string")]
            public void ResponseObject()
            {
                var response = new object[] { 
                    200, 
                    new Dictionary<string, string>()
                    {
                        { "Content-Type", "text/plain" }
                    }, 
                    "Hello world." 
                };

                response.ToResponse();
            }
        }
    }
}
