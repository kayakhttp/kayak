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
        InvocationInfo info;
        List<object> arguments;

        Mock<IKayakContext> mockContext;
        Mock<IInvocationBehavior> mockBehavior;
        Mock<ISubject<object>> mockSubject;

        [SetUp]
        public void SetUp()
        {
            mockBehavior = new Mock<IInvocationBehavior>(MockBehavior.Strict);           
            mockContext = new Mock<IKayakContext>();
            mockSubject = new Mock<ISubject<object>>();
            arguments = new List<object>();
        }

        [Test]
        [ExpectedException(typeof(ArgumentNullException))]
        public void NullBehaviorException()
        {
            mockContext = new Mock<IKayakContext>();

            mockContext.Object.PerformInvocation(null);
        }

        void SetUpMockBehaviorBind()
        {
            SetUpMockBehaviorBind((new InvocationInfo[] { info }).ToObservable());
        }

        void SetUpMockBehaviorBind(IObservable<InvocationInfo> bind)
        {
            //mockBehavior
            //    .Setup(b => b.Bind(It.Is<IKayakContext>(c => c == mockContext.Object)))
            //    .Returns(bind);
        }

        void SetUpMockBehaviorInvoke()
        {
            //mockBehavior
            //    .Setup(b => b.Invoke(It.Is<IKayakContext>(c => c == mockContext.Object), It.IsAny<IObservable<object>>()))
            //    .Callback<IKayakContext, IObservable<object>>((c, o) => o.Subscribe(mockSubject.Object));
        }

        [Test]
        public void NullBinderException()
        {
            mockContext.DefaultValue = DefaultValue.Mock;
            var mockRequest = Mock.Get(mockContext.Object.Request);
            mockRequest.Setup(r => r.Begin()).Returns(Observable.Empty<Unit>());

            SetUpMockBehaviorBind(null);
            SetUpMockBehaviorInvoke();

            mockContext.Object.PerformInvocation(mockBehavior.Object);

            mockRequest.VerifyAll();
            mockBehavior.VerifyAll();
            mockSubject.Verify(s => s.OnError(It.Is<Exception>(e => e.Message == "No binder was returned by the behavior.")));
        }

        [Test]
        public void NullInfoException()
        {
            mockContext.DefaultValue = DefaultValue.Mock;
            var mockRequest = Mock.Get(mockContext.Object.Request);
            mockRequest.Setup(r => r.Begin()).Returns(Observable.Empty<Unit>());

            SetUpMockBehaviorBind(Observable.Empty<InvocationInfo>());
            SetUpMockBehaviorInvoke();

            mockContext.Object.PerformInvocation(mockBehavior.Object);

            mockRequest.VerifyAll();
            mockBehavior.VerifyAll();
            mockSubject.Verify(s => s.OnError(It.Is<Exception>(e => e.Message == "Binder returned by behavior did not yield an instance of InvocationInfo.")));
        }

        [Test]
        public void NullInfoMethodException()
        {
            mockContext.DefaultValue = DefaultValue.Mock;
            var mockRequest = Mock.Get(mockContext.Object.Request);
            mockRequest.Setup(r => r.Begin()).Returns(Observable.Empty<Unit>());

            SetUpMockBehaviorBind(Observable.Empty<InvocationInfo>());
            SetUpMockBehaviorInvoke();

            info = new InvocationInfo();

            mockContext.Object.PerformInvocation(mockBehavior.Object);

            mockRequest.VerifyAll();
            mockBehavior.VerifyAll();
            mockSubject.Verify(s => s.OnError(It.Is<Exception>(e => e.Message == "Binder returned by behavior did not yield an instance of InvocationInfo.")));
        }

        [Test]
        public void NullInfoTargetException()
        {
            mockContext.DefaultValue = DefaultValue.Mock;
            var mockRequest = Mock.Get(mockContext.Object.Request);
            mockRequest.Setup(r => r.Begin()).Returns(Observable.Empty<Unit>());

            SetUpMockBehaviorBind(Observable.Empty<InvocationInfo>());
            SetUpMockBehaviorInvoke();

            var mockTarget = new Mock<IBind>();
            info = new InvocationInfo();
            info.Method = mockTarget.Object.GetType().GetMethod("Bind0");

            mockContext.Object.PerformInvocation(mockBehavior.Object);

            mockRequest.VerifyAll();
            mockBehavior.VerifyAll();
            mockSubject.Verify(s => s.OnError(It.Is<Exception>(e => e.Message == "Binder returned by behavior did not yield an instance of InvocationInfo.")));
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
            mockContext.DefaultValue = DefaultValue.Mock;
            var mockRequest = Mock.Get(mockContext.Object.Request);
            mockRequest.Setup(r => r.Begin()).Returns(Observable.Empty<Unit>());

            SetUpMockBehaviorBind();
            SetUpMockBehaviorInvoke();
            
            var mockTarget = new Mock<IBind>();
            InvokeMethod(mockTarget.Object, "Bind0");

            mockRequest.VerifyAll();
            mockBehavior.VerifyAll();
            mockTarget.Verify(m => m.Bind0());
            mockSubject.Verify(s => s.OnCompleted());
        }

        [Test]
        public void Bind1()
        {
            mockContext.DefaultValue = DefaultValue.Mock;
            var mockRequest = Mock.Get(mockContext.Object.Request);
            mockRequest.Setup(r => r.Begin()).Returns(Observable.Empty<Unit>());

            SetUpMockBehaviorBind();
            SetUpMockBehaviorInvoke();

            var mockTarget = new Mock<IBind>();
            object result = new object();
            mockTarget.Setup(t => t.Bind1()).Returns(result);
            
            InvokeMethod(mockTarget.Object, "Bind1");

            mockRequest.VerifyAll();
            mockBehavior.VerifyAll();
            mockTarget.Verify(m => m.Bind1());
            mockSubject.Verify(s => s.OnNext(It.Is<object>(o => o == result)));
            mockSubject.Verify(s => s.OnCompleted());
        }

        [Test]
        public void Throw0()
        {
            mockContext.DefaultValue = DefaultValue.Mock;
            var mockRequest = Mock.Get(mockContext.Object.Request);
            mockRequest.Setup(r => r.Begin()).Returns(Observable.Empty<Unit>());

            SetUpMockBehaviorBind();
            SetUpMockBehaviorInvoke();

            var mockTarget = new Mock<IBind>();
            var exception = new Exception();
            mockTarget.Setup(m => m.Throw0()).Throws(exception);

            InvokeMethod(mockTarget.Object, "Throw0");

            mockRequest.VerifyAll();
            mockBehavior.VerifyAll();
            mockTarget.VerifyAll();
            mockSubject.Verify(s => s.OnError(It.Is<Exception>(e => e.GetType() == typeof(TargetInvocationException) && e.InnerException == exception)));
        }

        [Test]
        public void Args0()
        {
            mockContext.DefaultValue = DefaultValue.Mock;
            var mockRequest = Mock.Get(mockContext.Object.Request);
            mockRequest.Setup(r => r.Begin()).Returns(Observable.Empty<Unit>());

            SetUpMockBehaviorBind();
            SetUpMockBehaviorInvoke();

            var mockTarget = new Mock<IBind>();
            var arg0 = new object();
            arguments.Add(arg0);
            mockTarget.Setup(m => m.Args0(It.Is<object>(o => o == arg0)));

            InvokeMethod(mockTarget.Object, "Args0");

            mockRequest.VerifyAll();
            mockBehavior.VerifyAll();
            mockTarget.VerifyAll();
            mockSubject.Verify(s => s.OnCompleted());
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

            SetUpMockBehaviorBind();

            mockContext.Object.PerformInvocation(mockBehavior.Object);
        }
    }
}
