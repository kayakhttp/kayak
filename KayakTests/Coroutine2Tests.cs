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
using System.Threading;
using System.Concurrency;

namespace KayakTests
{
    [TestFixture]
    public class Coroutine2Tests
    {
        [Test]
        public void ReturnsResult()
        {
            var scheduler = new SingleThreadedScheduler();
            Mock<IDisposable> mockDisposable = new Mock<IDisposable>();

            scheduler.Start();
            var task = ReturnsResultBlock(mockDisposable.Object).AsCoroutine2<string>(TaskCreationOptions.None, scheduler);
            scheduler.Dispose();

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
        public void ImmediateException()
        {
            var scheduler = new SingleThreadedScheduler();
            Mock<IDisposable> mockDisposable = new Mock<IDisposable>();
            var exception = new Exception("oops");

            Task task = null;
            Exception caughtException = null;

            scheduler.Start();
            try
            {
                var block = ImmediateExceptionBlock(exception, mockDisposable.Object);

                task = block.AsCoroutine2<string>(scheduler);
            }
            catch (Exception e)
            {
                caughtException = e;
            }
            finally
            {
                scheduler.Dispose();
            }

            mockDisposable.Verify(d => d.Dispose(), Times.Once(), "Disposable not disposed.");
            Assert.AreEqual(exception, caughtException, "Exceptions differ.");
        }

        IEnumerable<object> ImmediateExceptionBlock(Exception exception, IDisposable disposable)
        {
            using (disposable)
            {
                Console.WriteLine("throwing exception.");
                throw exception;
            }
        }

        [Test]
        public void DeferredException()
        {
            var scheduler = new SingleThreadedScheduler();
            Mock<IDisposable> mockDisposable = new Mock<IDisposable>();
            var exception = new Exception("oops");

            Task task = null;
            Exception caughtException = null;

            scheduler.Start();
            try
            {
                task = DeferredExceptionBlock(exception, mockDisposable.Object).CreateCoroutine<string>(scheduler);
            }
            catch (Exception e)
            {
                caughtException = e;
            }
            finally
            {
                scheduler.Dispose();
            }

            mockDisposable.Verify(d => d.Dispose(), Times.Once(), "Disposable not disposed.");
            Assert.IsNull(caughtException, "Coroutine constructor threw up.");
            Assert.IsNotNull(task.Exception, "Coroutine didn't have exception.");
            Assert.AreEqual(exception, task.Exception.InnerException, "Exceptions differ.");
        }

        IEnumerable<object> DeferredExceptionBlock(Exception exception, IDisposable disposable)
        {
            using (disposable)
            {
                yield return null;
                throw exception;
            }
        }

        [Test]
        public void RunsTask()
        {
            var scheduler = new SingleThreadedScheduler();
            Mock<IDisposable> mockDisposable = new Mock<IDisposable>();

            bool taskRun = false;
            Action subTask = () =>
                {
                    Console.WriteLine("Sub task run.");
                    taskRun = true;
                };



            scheduler.Start();
            var task = RunsTaskBlock(subTask, mockDisposable.Object).CreateCoroutine<int>(scheduler);
            Thread.SpinWait(10000);
            scheduler.Dispose();

            mockDisposable.Verify(d => d.Dispose(), Times.Once());
            Assert.IsTrue(task.IsCompleted, "Not IsCompleted");
            Assert.IsFalse(task.IsFaulted, "IsFaulted");
            Assert.IsTrue(taskRun, "Task was not run.");
        }

        IEnumerable<object> RunsTaskBlock(Action task, IDisposable disposable)
        {
            using (disposable)
            {
                yield return Task.Factory.StartNew(task, CancellationToken.None, TaskCreationOptions.None, TaskScheduler.Current);
            }
        }

        // disabled, no good if we want to be able to catch errors.
        //[Test]
        public void PropagatesExceptionFromTask()
        {
            var scheduler = new SingleThreadedScheduler();
            Mock<IDisposable> mockDisposable = new Mock<IDisposable>();

            Exception exception = null;

            Action subTask = () => {
                exception = new Exception("oops.");
                throw exception;
            };

            scheduler.Start();
            var task = PropagatesExceptionFromTaskBlock(subTask, mockDisposable.Object).CreateCoroutine<int>(scheduler);
            Thread.SpinWait(10000);
            scheduler.Dispose();

            mockDisposable.Verify(d => d.Dispose(), Times.Once());
            Assert.IsTrue(task.IsCompleted, "Not IsCompleted");
            Assert.IsTrue(task.IsFaulted, "Not IsFaulted");

            //                              Aggregate Aggregate      exception
            Assert.AreEqual(exception, task.Exception.InnerException.InnerException);
        }

        IEnumerable<object> PropagatesExceptionFromTaskBlock(Action task, IDisposable disposable)
        {
            using (disposable)
            {
                yield return Task.Factory.StartNew(task, CancellationToken.None, TaskCreationOptions.None, TaskScheduler.Current);
            }
        }

        [Test]
        public void PropagatesValueFromTask()
        {
            var scheduler = new SingleThreadedScheduler();
            Mock<IDisposable> mockDisposable = new Mock<IDisposable>();

            int value = 42;

            Func<int> subTask = () => { return value; };

            scheduler.Start();
            var task = PropagatesValuesFromTaskBlock(subTask, mockDisposable.Object).CreateCoroutine<int>(scheduler);
            Thread.SpinWait(10000);
            scheduler.Dispose();

            mockDisposable.Verify(d => d.Dispose(), Times.Once());
            Assert.IsTrue(task.IsCompleted, "Not IsCompleted");
            Assert.IsFalse(task.IsFaulted, "IsFaulted");
            Assert.AreEqual(value, task.Result, "Values differ.");
        }

        IEnumerable<object> PropagatesValuesFromTaskBlock(Func<int> func, IDisposable disposable)
        {
            using (disposable)
            {
                var task = Task.Factory.StartNew(func, CancellationToken.None, TaskCreationOptions.None, TaskScheduler.Current);
                yield return task;
                yield return task.Result;
            }
        }

        [Test]
        public void SetsSchedulerImplicitly()
        {
            var scheduler = new SingleThreadedScheduler();

            scheduler.Start();
            var task = SetsSchedulerImplicitlyBlock(scheduler).CreateCoroutine<TaskScheduler>(scheduler);
            Thread.SpinWait(10000);
            scheduler.Dispose();

            Assert.IsTrue(task.IsCompleted, "Not IsCompleted");
            Assert.IsFalse(task.IsFaulted, "IsFaulted");
        }

        IEnumerable<object> SetsSchedulerImplicitlyBlock(TaskScheduler scheduler)
        {
            Assert.AreEqual(scheduler, TaskScheduler.Current);
            yield break;
        }

        [Test]
        public void ReturnsResultFromNestedCoroutine()
        {
            var scheduler = new SingleThreadedScheduler();

            scheduler.Start();
            var task = ReturnsResultFromNestedCoroutineBlock().CreateCoroutine<int>(scheduler);
            Thread.SpinWait(10000);
            scheduler.Dispose();

            Assert.IsTrue(task.IsCompleted);
            Assert.IsFalse(task.IsFaulted);
            Assert.AreEqual(52, task.Result);
        }

        IEnumerable<object> ReturnsResultFromNestedCoroutineBlock()
        {
            Assert.AreEqual(typeof(SingleThreadedScheduler), TaskScheduler.Current.GetType());
            var inner = ReturnsResultFromNestedCoroutineBlockInner().AsCoroutine2<int>();
            yield return inner;
            yield return inner.Result;
        }

        IEnumerable<object> ReturnsResultFromNestedCoroutineBlockInner()
        {
            yield return 52;
        }

        [Test]
        public void PropagatesExceptionFromNestedCoroutine()
        {
            var scheduler = new SingleThreadedScheduler();
            Exception e = new Exception("Boo.");

            scheduler.Start();
            var task = PropagatesExceptionFromNestedCoroutineBlock(e).AsCoroutine2<int>();
            Thread.SpinWait(10000);
            scheduler.Dispose();

            Assert.IsTrue(task.IsCompleted);
            Assert.IsTrue(task.IsFaulted);
            Assert.AreEqual(e, task.Exception.InnerException);
        }

        IEnumerable<object> PropagatesExceptionFromNestedCoroutineBlock(Exception e)
        {
            var inner = PropagatesExceptionFromNestedCoroutineBlockInner(e).AsCoroutine2<int>();
            yield return inner;
            if (inner.Exception != null)
                throw inner.Exception;

            yield return inner.Result;
        }

        IEnumerable<object> PropagatesExceptionFromNestedCoroutineBlockInner(Exception e)
        {
            throw e;
        }
    }
}
