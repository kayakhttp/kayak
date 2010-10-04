using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit;
using Kayak;
using NUnit.Framework;

namespace KayakTests.Extensions
{
    [TestFixture]
    public class ResponseTests
    {
        [Test]
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
