//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;
//using NUnit.Core;
//using Kayak;
//using Kayak.Framework;
//using NUnit.Framework;
//using System.Reflection;
//using System.Disposables;
//using Moq;

//namespace KayakTests.Framework
//{
//    // test that it 
//    // - retrieves the InvocationInfo from the bind observable
//    // - if no method is provided, nothing happens
//    // - invokes the method with the expected arguments
//    // - OnError is called if exception is thrown
//    // - OnNext is called with value (null if void or nothing? probably nothing)
//    // - OnCompleted is called regardless.
//    [TestFixture]
//    public class KayakInvocationTests
//    {
//        MethodInfo method;
//        List<object> arguments;
//        Subject<InvocationInfo> bind;

//        KayakInvocation invocation;
//        bool gotResult, gotException;
//        object result, expectedResult;
//        Exception exception, expectedException;
//        int completed;


//        [SetUp]
//        public void SetUp()
//        {
//            arguments = new List<object>();
//            bind = new Subject<InvocationInfo>();
//            gotResult = false;
//            result = null;
//            exception = null;
//            completed = 0;
//        }

//        public interface IBind
//        {
//            void Bind0();
//            object Bind1();
//            void Throw0();
//            void Args0(object arg);
//        }

//        [Test]
//        public void Bind0()
//        {
//            var mock = new Mock<IBind>();
//            mock.Setup(m => m.Bind0()).Verifiable();

//            InvokeMethod(mock.Object, "Bind0");

//            mock.Verify();

//            AssertNoResult();
//            AssertNoException();
//            AssertCompleted();
//        }

//        [Test]
//        public void Bind1()
//        {
//            expectedResult = new object();

//            var mock = new Mock<IBind>();
//            mock.Setup(m => m.Bind1()).Returns(expectedResult).Verifiable();

//            InvokeMethod(mock.Object, "Bind1");

//            mock.Verify();

//            AssertResult();
//            AssertNoException();
//            AssertCompleted();
//        }

//        [Test]
//        public void Throw0()
//        {
//            expectedException = new Exception();

//            var mock = new Mock<IBind>();
//            mock.Setup(m => m.Throw0()).Throws(expectedException).Verifiable();

            
//            InvokeMethod(mock.Object, "Throw0");

//            mock.Verify();

//            AssertNoResult();
//            AssertException();
//            AssertNotCompleted();
//        }

//        [Test]
//        public void Args0()
//        {
//            var arg0 = new object();

//            var mock = new Mock<IBind>();
//            mock.Setup(m => m.Args0(It.Is<object>(a => a == arg0))).Verifiable();

//            arguments.Add(arg0);

//            InvokeMethod(mock.Object, "Args0");

//            mock.Verify();

//            AssertNoResult();
//            AssertNoException();
//            AssertCompleted();
//        }

//        void AssertNoResult()
//        {
//            Assert.IsFalse(gotResult, "Did not expect result.");
//        }

//        void AssertResult()
//        {
//            Assert.IsTrue(gotResult, "Expected result.");
//            Assert.AreEqual(expectedResult, result, "Unexpected result.");
//        }

//        void AssertNoException()
//        {
//            Assert.IsFalse(gotException, "Did not expect exception.");
//        }

//        void AssertException()
//        {
//            Assert.IsTrue(gotException, "Expected exception.");
//            Assert.IsInstanceOfType(typeof(TargetInvocationException), exception);
//            Assert.AreEqual(expectedException, exception.InnerException, "Unexpected exception.");
//        }

//        void AssertCompleted()
//        {
//            Assert.AreEqual(1, completed, "Did not get completed exactly once.");
//        }

//        void AssertNotCompleted()
//        {
//            Assert.AreEqual(0, completed, "Did not expect to get completed.");
//        }

//        void InvocationSubscribe()
//        {
//            invocation.Subscribe(o => { gotResult = true; result = o; }, e => { gotException = true; exception = e; }, () => completed++);
//        }

//        void InvokeMethod(object target, string name)
//        {
//            var method = target.GetType().GetMethod(name);

//            if (method == null)
//                throw new Exception("Unknown method '" + name + "'");
            
//            invocation = new KayakInvocation(null, bind);
//            InvocationSubscribe();

//            bind.OnNext(new InvocationInfo()
//            {
//                Target = target,
//                Method = method,
//                Arguments = arguments.ToArray()
//            });
//            bind.OnCompleted(); 
//        }

//        // error during bind should cause the exception to be forwarded to the context.
//        [Test]
//        public void BindContextError()
//        {
//            Exception exceptionToThrow = new Exception();

//            var mockContext = new Mock<IKayakContext>();
//            mockContext.Setup(c => c.OnError(It.Is<Exception>(e => e == exceptionToThrow))).Verifiable();
//            invocation = new KayakInvocation(mockContext.Object, bind);

//            InvocationSubscribe();

//            bind.OnError(exceptionToThrow);

//            mockContext.Verify();

//            AssertNoException();
//            AssertNoResult();
//            AssertNotCompleted();
//        }
//    }
//}
