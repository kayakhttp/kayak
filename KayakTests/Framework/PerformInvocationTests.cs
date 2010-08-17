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

        bool gotResult, handlerGotResult, gotException, handlerGotException;
        object result, expectedResult, handlerResult, expectedHandlerResult;
        Exception exception, expectedException, handlerException, expectedHandlerException;
        int completed, handlerCompleted;

        [SetUp]
        public void SetUp()
        {
            mockBehavior = new Mock<IInvocationBehavior>();
            mockContext = new Mock<IKayakContext>();

            arguments = new List<object>();

            gotResult = handlerGotResult = gotException = handlerGotException = false;

            result = expectedResult = handlerResult = expectedHandlerResult
                = exception = expectedException = handlerException = expectedHandlerException = null;

            completed = handlerCompleted = 0;
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

            //mockContext.Setup(c => c.OnError(It.Is<Exception>(e => matches(e)))).Verifiable();
        }

        void Invoke()
        {
            mockContext.Object.PerformInvocation(mockBehavior.Object)
                .Subscribe(u => gotResult = true, e => { gotException = true; exception = e; }, () => completed++);
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
            SetUpMockBehaviorGetBinder(null);

            Invoke();

            AssertExceptionMessage("Behavior returned null binder.");

            mockContext.VerifyAll();
            mockBehavior.VerifyAll();
        }

        [Test]
        public void NullInfoException()
        {
            info = null;

            SetUpMockBehaviorGetBinder();
            
            Invoke();

            AssertExceptionMessage("Binder returned by behavior did not yield an instance of InvocationInfo.");
            
            mockContext.VerifyAll();
            mockBehavior.VerifyAll();
        }

        [Test]
        public void NullInfoMethodException()
        {
            info = new InvocationInfo();

            SetUpMockBehaviorGetBinder();

            Invoke();

            AssertExceptionMessage("Binder returned by behavior did not yield an valid instance of InvocationInfo. Method was null.");

            mockContext.VerifyAll();
            mockBehavior.VerifyAll();
        }

        [Test]
        public void NullInfoTargetException()
        {
            info = new InvocationInfo();

            var mockTarget = new Mock<IBind>();
            info.Method = mockTarget.Object.GetType().GetMethod("Bind0");

            SetUpMockBehaviorGetBinder();

            Invoke();

            AssertExceptionMessage("Binder returned by behavior did not yield an valid instance of InvocationInfo. Target was null.");

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

            SetUpMockBehaviorGetBinder();
            SetUpMockBehaviorGetHandler(null);

            Invoke();


            AssertExceptionMessage("Behavior returned null handler.");

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

            AssertNoHandlerResult();
            AssertNoHandlerException();
            AssertHandlerCompleted();
        }

        [Test]
        public void Bind1()
        {
            expectedHandlerResult = new object();

            var mock = new Mock<IBind>();
            mock.Setup(m => m.Bind1()).Returns(expectedHandlerResult).Verifiable();

            InvokeMethod(mock.Object, "Bind1");

            mock.Verify();

            AssertNoResult();
            AssertNoException();
            AssertCompleted();

            AssertHandlerResult();
            AssertNoHandlerException();
            AssertHandlerCompleted();
        }

        [Test]
        public void Throw0()
        {
            expectedHandlerException = new Exception();

            var mock = new Mock<IBind>();
            mock.Setup(m => m.Throw0()).Throws(expectedHandlerException).Verifiable();

            InvokeMethod(mock.Object, "Throw0");

            mock.Verify();

            AssertNoResult();
            AssertNoException();
            AssertCompleted();

            AssertNoHandlerResult();
            AssertHandlerException();
            AssertHandlerNotCompleted();
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

            AssertNoHandlerResult();
            AssertNoHandlerException();
            AssertHandlerCompleted();
        }

        void AssertNoResult()
        {
            Assert.IsFalse(gotResult, "Did not expect result.");
        }

        void AssertNoHandlerResult()
        {
            Assert.IsFalse(handlerGotResult, "Did not expect result.");
        }

        void AssertHandlerResult()
        {
            Assert.IsTrue(handlerGotResult, "Handler expected result.");
            Assert.AreEqual(expectedHandlerResult, handlerResult, "Handler got unexpected result.");
        }

        void AssertNoException()
        {
            Assert.IsFalse(gotException, "Did not expect exception.");
        }

        void AssertNoHandlerException()
        {
            Assert.IsFalse(handlerGotException, "Did not expect exception.");
        }

        void AssertHandlerException()
        {
            Assert.IsTrue(handlerGotException, "Handler expected exception.");
            Assert.IsInstanceOfType(typeof(TargetInvocationException), handlerException);
            Assert.AreEqual(expectedHandlerException, handlerException.InnerException, "Handler got unexpected exception.");
        }

        void AssertExceptionMessage(string expectedErrorMessage)
        {
            Assert.AreEqual(expectedErrorMessage, exception.Message);
        }

        void AssertCompleted()
        {
            Assert.AreEqual(1, completed, "Did not get completed exactly once.");
        }

        void AssertNotCompleted()
        {
            Assert.AreEqual(0, completed, "Did not expect to get completed.");
        }

        void AssertHandlerCompleted()
        {
            Assert.AreEqual(1, handlerCompleted, "Handler did not get completed exactly once.");
        }

        void AssertHandlerNotCompleted()
        {
            Assert.AreEqual(0, handlerCompleted, "Handler did not expect to get completed.");
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
                Observer.Create<object>(o => { handlerGotResult = true; handlerResult = o; }, e => { handlerGotException = true; handlerException = e; }, () => handlerCompleted++)
                );

            Invoke();
        }
    }
}
