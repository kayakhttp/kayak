using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using NUnit.Core;
using Kayak;
using Moq;

namespace KayakTests.Framework
{
    [TestFixture]
    public class HeaderBinderTests
    {
        [Test]
        public void BindArgumentsFromBodyNoOp()
        {
            // noop!
        }

        [Test]
        public void BindArgumentsFromHeaders()
        {
            // doesn't bind params from [RequestBody]
            // path params from MethodMap precede params from query string
            // matches output from Coerce.

        }
    }
}
