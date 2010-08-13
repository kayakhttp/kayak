using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Core;
using NUnit.Framework;
using Kayak;
using Moq;
using Kayak.Framework;
using System.Reflection;

namespace KayakTests.Framework
{
    [TestFixture]
    public class PerformInvocationTests
    {
        MethodInfo method;
        InvocationInfo info;
        List<object> arguments;

        Mock<IKayakContext> mockContext;
        Mock<IInvocationBehavior> mockBehavior;

        bool gotResult, gotException;
        object result, expectedResult;
        Exception exception, expectedException;
        int completed;

        [SetUp]
        public void SetUp()
        {
            mockBehavior = new Mock<IInvocationBehavior>();
            mockContext = new Mock<IKayakContext>();

            arguments = new List<object>();
            gotResult = false;
            result = null;
            gotException = false;
            exception = null;
            completed = 0;
        }

        [Test]
        [ExpectedException(typeof(ArgumentNullException))]
        public void NullBehaviorException()
        {
            mockContext = new Mock<IKayakContext>();
            mockContext.Object.PerformInvocation(null);
        }

        void SetUpExpectedContextErrorMessage(string expectedErrorMessage)
        {
            Func<Exception, bool> matches = e => 
                { 
                    if (e.Message == expectedErrorMessage) return true; 
                    else Console.WriteLine("Unexpected exception message " + e.Message); 
                    return false; 
                };

            mockContext.Setup(c => c.OnError(It.Is<Exception>(e => matches(e)))).Verifiable();
        }

        void SetUpMockBehaviorGetBinder()
        {
            SetUpMockBehaviorGetBinder((new InvocationInfo[] { info }).ToObservable());
        }

        void SetUpMockBehaviorGetBinder(IObservable<InvocationInfo> bind)
        {
            mockBehavior
                .Setup(b => b.GetBinder(It.Is<IKayakContext>(c => c == mockContext.Object)))
                .Returns(bind).Verifiable();
        }

        void SetUpMockBehaviorGetHandler(IObserver<object> handler)
        {
            mockBehavior
                .Setup(b => b.GetHandler(It.Is<IKayakContext>(c => c == mockContext.Object), It.Is<InvocationInfo>(ii => ii == info)))
                .Returns(handler)
                .Verifiable();
        }

        [Test]
        public void NullBinderException()
        {
            SetUpExpectedContextErrorMessage("Behavior returned null binder.");
            SetUpMockBehaviorGetBinder(null);

            mockContext.Object.PerformInvocation(mockBehavior.Object);

            mockContext.VerifyAll();
            mockBehavior.VerifyAll();
        }

        [Test]
        public void NullInfoException()
        {
            info = null;

            SetUpExpectedContextErrorMessage("Binder returned by behavior did not yield an instance of InvocationInfo.");
            SetUpMockBehaviorGetBinder();

            mockContext.Object.PerformInvocation(mockBehavior.Object);
            
            mockContext.VerifyAll();
            mockBehavior.VerifyAll();
        }

        [Test]
        public void NullInfoMethodException()
        {
            info = new InvocationInfo();

            SetUpExpectedContextErrorMessage("Binder returned by behavior did not yield an valid instance of InvocationInfo. Method was null.");
            SetUpMockBehaviorGetBinder();

            mockContext.Object.PerformInvocation(mockBehavior.Object);

            mockContext.VerifyAll();
            mockBehavior.VerifyAll();
        }

        [Test]
        public void NullInfoTargetException()
        {
            info = new InvocationInfo();

            var mockTarget = new Mock<IBind>();
            info.Method = mockTarget.Object.GetType().GetMethod("Bind0");

            SetUpExpectedContextErrorMessage("Binder returned by behavior did not yield an valid instance of InvocationInfo. Target was null.");
            SetUpMockBehaviorGetBinder();

            mockContext.Object.PerformInvocation(mockBehavior.Object);

            mockContext.VerifyAll();
            mockBehavior.VerifyAll();
        }

        [Test]
        public void NullHandlerException()
        {
            info = new InvocationInfo();

            var mockTarget = new Mock<IBind>();
            info.Method = mockTarget.Object.GetType().GetMethod("Bind0");
            info.Target = mockTarget.Object;

            SetUpExpectedContextErrorMessage("Behavior returned null handler.");
            SetUpMockBehaviorGetBinder();
            SetUpMockBehaviorGetHandler(null);

            mockContext.Object.PerformInvocation(mockBehavior.Object);

            mockContext.VerifyAll();
            mockBehavior.VerifyAll();
        }

        public interface IBind
        {
            void Bind0();
            object Bind1();
            void Throw0();
            void Args0(object arg);
        }

        [Test]
        public void Bind0()
        {
            var mock = new Mock<IBind>();
            mock.Setup(m => m.Bind0()).Verifiable();

            InvokeMethod(mock.Object, "Bind0");

            mock.Verify();

            AssertNoResult();
            AssertNoException();
            AssertCompleted();
        }

        [Test]
        public void Bind1()
        {
            expectedResult = new object();

            var mock = new Mock<IBind>();
            mock.Setup(m => m.Bind1()).Returns(expectedResult).Verifiable();

            InvokeMethod(mock.Object, "Bind1");

            mock.Verify();

            AssertResult();
            AssertNoException();
            AssertCompleted();
        }

        [Test]
        public void Throw0()
        {
            expectedException = new Exception();

            var mock = new Mock<IBind>();
            mock.Setup(m => m.Throw0()).Throws(expectedException).Verifiable();

            InvokeMethod(mock.Object, "Throw0");

            mock.Verify();

            AssertNoResult();
            AssertException();
            AssertNotCompleted();
        }

        [Test]
        public void Args0()
        {
            var arg0 = new object();

            var mock = new Mock<IBind>();
            mock.Setup(m => m.Args0(It.Is<object>(a => a == arg0))).Verifiable();

            arguments.Add(arg0);

            InvokeMethod(mock.Object, "Args0");

            mock.Verify();

            AssertNoResult();
            AssertNoException();
            AssertCompleted();
        }

        void AssertNoResult()
        {
            Assert.IsFalse(gotResult, "Did not expect result.");
        }

        void AssertResult()
        {
            Assert.IsTrue(gotResult, "Expected result.");
            Assert.AreEqual(expectedResult, result, "Unexpected result.");
        }

        void AssertNoException()
        {
            Assert.IsFalse(gotException, "Did not expect exception.");
        }

        void AssertException()
        {
            Assert.IsTrue(gotException, "Expected exception.");
            Assert.IsInstanceOfType(typeof(TargetInvocationException), exception);
            Assert.AreEqual(expectedException, exception.InnerException, "Unexpected exception.");
        }

        void AssertCompleted()
        {
            Assert.AreEqual(1, completed, "Did not get completed exactly once.");
        }

        void AssertNotCompleted()
        {
            Assert.AreEqual(0, completed, "Did not expect to get completed.");
        }

        void InvocationSubscribe()
        {
            //invocation.Subscribe(o => { gotResult = true; result = o; }, e => { gotException = true; exception = e; }, () => completed++);
        }

        void InvokeMethod(object target, string name)
        {
            var method = target.GetType().GetMethod(name);

            if (method == null)
                throw new Exception("Unknown method '" + name + "'");

            info = new InvocationInfo()
            {
                Target = target,
                Method = method,
                Arguments = arguments.ToArray()
            };

            mockContext = new Mock<IKayakContext>();

            SetUpMockBehaviorGetBinder();
            SetUpMockBehaviorGetHandler(
                Observer.Create<object>(o => { gotResult = true; result = o; }, e => { gotException = true; exception = e; }, () => completed++)
                );
            

            mockContext.Object.PerformInvocation(mockBehavior.Object);
        }
    }
}
