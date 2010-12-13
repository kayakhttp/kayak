using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Core;
using System.Threading.Tasks;
using Kayak;
using NUnit.Framework;
using Moq;
using System.Disposables;

namespace KayakTests
{
    [TestFixture]
    public class Coroutine2Tests
    {
        [Test]
        public void ReturnsResult()
        {
            Mock<IDisposable> mockDisposable = new Mock<IDisposable>();

            var task = ReturnsResultBlock(mockDisposable.Object).AsCoroutine2<string>();

            mockDisposable.Verify(d => d.Dispose(), Times.Once(), "Disposable not disposed.");
            Assert.IsTrue(task.IsCompleted, "Not IsCompleted");
            Assert.IsFalse(task.IsFaulted, "IsFaulted");
            Assert.AreEqual("result", task.Result);
        }

        IEnumerable<object> ReturnsResultBlock(IDisposable disposable)
        {
            using (disposable)
            {
                yield return null; // no op;
                yield return new object(); // discarded
                yield return "result";
            }
        }

        [Test]
        public void HasException()
        {
            var exception = new Exception("oops");
            Mock<IDisposable> mockDisposable = new Mock<IDisposable>();

            Exception caughtException = null;
            try
            {
                HasExceptionBlock(exception, mockDisposable.Object).AsCoroutine2<string>();
            }
            catch (Exception e) 
            {
                caughtException = e;
            }

            mockDisposable.Verify(d => d.Dispose(), Times.Once(), "Disposable not disposed.");
            Assert.AreEqual(exception, caughtException, "Exceptions differ.");
        }

        IEnumerable<object> HasExceptionBlock(Exception exception, IDisposable disposable)
        {
            using (disposable)
            {
                throw exception;
            }
        }

        [Test]
        public void RunsTask()
        {
            bool taskRun = false;
            var subTask = new Task(() => taskRun = true);

            Mock<IDisposable> mockDisposable = new Mock<IDisposable>();
            var task = RunsTaskBlock(subTask, mockDisposable.Object).AsCoroutine2<Unit>();

            mockDisposable.Verify(d => d.Dispose(), Times.Once());
            Assert.IsTrue(task.IsCompleted, "Not IsCompleted");
            Assert.IsFalse(task.IsFaulted, "IsFaulted");
            Assert.IsTrue(taskRun, "Task was not run.");
        }

        IEnumerable<object> RunsTaskBlock(Task task, IDisposable disposable)
        {
            using (disposable)
            {
                yield return task;
            }
        }

        [Test]
        public void PropagatesExceptionFromTask()
        {
            Console.WriteLine("Begin PropagatesExceptionFromTask");
            Exception exception = null;

            var subTask = new Task(() => {
                exception = new Exception("oops.");
                throw exception;
            });

            Mock<IDisposable> mockDisposable = new Mock<IDisposable>();
            //mockDisposable.Setup(d => d.Dispose()).Callback(() => Console.WriteLine("Disposable disposed."));
            
            Exception caughtException = null;
            Task task = null;
            try
            {
                task = PropagatesExceptionFromTaskBlock(subTask, mockDisposable.Object).AsCoroutine2<Unit>();
            }
            catch (Exception e)
            {
                caughtException = e;
            }

            Console.WriteLine("End PropagatesExceptionFromTask");
            mockDisposable.Verify(d => d.Dispose(), Times.Once());
            Assert.IsNull(caughtException);
            Assert.IsTrue(task.IsCompleted, "Not IsCompleted");
            Assert.IsTrue(task.IsFaulted, "Not IsFaulted");
            Assert.AreEqual(exception, subTask.Exception.InnerException);
            Assert.AreEqual(exception, task.Exception.InnerException.InnerException);

        }

        IEnumerable<object> PropagatesExceptionFromTaskBlock(Task task, IDisposable disposable)
        {
            using (disposable)
            {
                yield return task;
            }
        }
    }
}
