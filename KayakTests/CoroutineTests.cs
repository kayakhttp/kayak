using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Core;
using NUnit.Framework;
using Kayak;

namespace KayakTests
{
    [TestFixture]
    public class CoroutineTests
    {
        object t1, t2, t3, t4;
        ISubject<object> subject;
        Exception e1;

        Coroutine coroutine;

        List<object> results, expectedResults;
        List<Exception> errors, expectedErrors;
        int completed;
        
        [SetUp]
        public void SetUp()
        {
            results = new List<object>();
            expectedResults = new List<object>();
            errors = new List<Exception>();
            expectedErrors = new List<Exception>();
            completed = 0;
            
            t1 = new object();
            t2 = new object();
            t3 = new object();
            t4 = new object();

            e1 = new Exception();
        }

        void Subscribe()
        {
            coroutine.Subscribe(o => results.Add(o), e => errors.Add(e), () => completed++);
        }

        [Test]
        public void YieldValues()
        {
            coroutine = YieldBlock().AsCoroutine();

            Subscribe();

            expectedResults.Add(t1);
            expectedResults.Add(t2);
            expectedResults.Add(t3);

            AssertExpectedResults();
            AssertExpectedErrors();
            AssertCompleted();
        }

        IEnumerable<object> YieldBlock()
        {
            yield return t1;
            yield return t2;
            yield return t3;
        }

        [Test]
        public void YieldObservable()
        {
            coroutine = YieldObservableBlock().AsCoroutine();

            subject = new Subject<object>();

            Subscribe();

            expectedResults.Add(t1);
            expectedResults.Add(t2);

            AssertExpectedResults();

            subject.OnNext(t3);
            expectedResults.Add(t3);

            AssertExpectedResults();

            subject.OnCompleted();
            expectedResults.Add(t4);

            AssertExpectedResults();
            AssertExpectedErrors();
            AssertCompleted();
        }

        IEnumerable<object> YieldObservableBlock()
        {
            yield return t1;
            yield return t2;
            yield return subject;
            yield return t4;
        }

        [Test]
        public void YieldThrow()
        {
            coroutine = YieldThrowBlock().AsCoroutine();

            Subscribe();

            expectedResults.Add(t1);
            expectedResults.Add(t2);
            
            expectedErrors.Add(e1);

            AssertExpectedResults();
            AssertExpectedErrors();
            AssertNotCompleted();
        }

        IEnumerable<object> YieldThrowBlock()
        {
            yield return t1;
            yield return t2;
            throw e1;
        }

        [Test]
        public void YieldObserableError()
        {
            coroutine = YieldObservableErrorBlock().AsCoroutine();

            subject = new Subject<object>();

            Subscribe();

            subject.OnNext(t2);
            subject.OnError(e1);

            expectedResults.Add(t1);
            expectedResults.Add(t2);
            expectedErrors.Add(e1);

            AssertExpectedResults();
            AssertExpectedErrors();
            AssertNotCompleted();
        }

        IEnumerable<object> YieldObservableErrorBlock()
        {
            yield return t1;
            yield return subject;
            yield return t3;
        }

        void AssertExpectedResults()
        {
            Assert.AreEqual(expectedResults.Count, results.Count, "Unexpected results count.");

            foreach (var i in Enumerable.Range(0, expectedResults.Count))
                Assert.AreEqual(expectedResults[i], results[i], "Unexpected result at index " + i + ".");
        }

        void AssertExpectedErrors()
        {
            Assert.AreEqual(expectedErrors.Count, errors.Count, "Unexpected error count.");

            foreach (var i in Enumerable.Range(0, expectedErrors.Count))
                Assert.AreEqual(expectedErrors[i], errors[i], "Unexpected error at index " + i + ".");
        }

        void AssertCompleted()
        {
            Assert.AreEqual(1, completed, "Unexpected number of calls to completed callback.");
        }

        void AssertNotCompleted()
        {
            Assert.AreEqual(0, completed, "Unexpected number of calls to completed callback.");
        }

        #region Observable behavior

        class WithEvent
        {
            public event EventHandler<EventArgs> Eventness;
            public void Raise()
            {
                if (Eventness != null)
                    Eventness(this, EventArgs.Empty);
            }
        }

        // turns out Observable.FromEvent never ends.
        //[Test]
        //public void EventTest()
        //{
        //    var with = new WithEvent();

        //    var seq = Observable.FromEvent<EventArgs>(h => with.Eventness += h , h => with.Eventness -= h);

        //    subject = new Subject<object>();

        //    var sx = seq.Select(e => e.EventArgs).Subscribe(e => results.Add(e), e => errors.Add(e), () => completed++);

        //    with.Raise();
        //    with.Raise();

        //    expectedResults.Add(EventArgs.Empty);
        //    expectedResults.Add(EventArgs.Empty);

        //    AssertExpectedResults();
        //    AssertNotCompleted();

        //    sx.Dispose();

        //    AssertNotCompleted();
        //}

        // Turns out that after an error callback, no other callbacks will be
        // received by observers.

        //[Test]
        //public void SubjectTest()
        //{
        //    var s = new Subject<Unit>();

        //    Exception error = null;
        //    bool completed = false, gotValue = false;
        //    s.Subscribe(u => gotValue = true, e => error = e, () => completed = true);

        //    s.OnError(new Exception());

        //    Assert.IsNotNull(error, "Error was null.");

        //    s.OnCompleted(); // no-op if preceeded by error!?
        //    s.OnNext(new Unit()); // same?

        //    Assert.IsFalse(completed, "Completed was true.");
        //    Assert.IsFalse(gotValue, "Got a value unexpectedly.");
        //}

        //[Test]
        //public void SubjectTest2()
        //{
        //    var s = new Subject<Unit>();

        //    Exception error = null;
        //    bool completed = false;
        //    s.Subscribe(u => { }, e => error = e, () => completed = true);

        //    s.OnCompleted();
        //    Assert.IsTrue(completed, "Completed was false.");
        //}

        //[Test]
        //public void SubjectTest3()
        //{
        //    var s = new Subject<Unit>();

        //    Exception error = null;
        //    bool completed = false, gotValue = false;
        //    s.Subscribe(u => gotValue = true, e => error = e, () => completed = true);

        //    s.OnError(new Exception());
        //    s.OnNext(new Unit());
        //    s.OnCompleted();

        //    Assert.IsNotNull(error, "Error was null.");
        //    Assert.IsFalse(completed, "Got completed");
        //    Assert.IsFalse(gotValue, "Got a value.");
        //}

        #endregion
    }
}
