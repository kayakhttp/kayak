using System;
using System.Threading;
using NUnit.Framework;

namespace Kayak.Tests.Net
{
    // TODO
    // - exceptions thrown on scheduler are handled in sane/predictable way.
    class SchedulerTests
    {
        ManualResetEventSlim wh;
        IScheduler scheduler;

        SchedulerDelegate schedulerDelegate;
      
        [SetUp]
        public void SetUp()
        {
            wh = new ManualResetEventSlim();

            schedulerDelegate = new SchedulerDelegate();
            schedulerDelegate.OnStoppedAction = () => wh.Set();
            scheduler = new KayakScheduler(schedulerDelegate);
        }

        [TearDown]
        public void TearDown()
        {
            wh.Dispose();
        }

        public void RunScheduler()
        {
            new Thread(() => scheduler.Start()).Start();
            wh.Wait(TimeSpan.FromSeconds(5));
        }

        //[Test]
        public void Double_start_throws_exception()
        {
            RunScheduler();

            Exception ex = null;
            try
            {
                scheduler.Start();
            }
            catch (Exception e)
            {
                ex = e;
            }

            Assert.That(ex, Is.Not.Null);
            Assert.That(ex.GetType(), Is.EqualTo(typeof(InvalidOperationException)));
            Assert.That(ex.Message, Is.EqualTo("The scheduler was already started."));
        }

        //[Test]
        public void Double_stop_throws_exception()
        {
            scheduler.Start();
            scheduler.Stop();

            Exception ex = null;
            try
            {
                scheduler.Stop();
            }
            catch (Exception e)
            {
                ex = e;
            }

            Assert.That(ex, Is.Not.Null);
            Assert.That(ex.GetType(), Is.EqualTo(typeof(InvalidOperationException)));
            Assert.That(ex.Message, Is.EqualTo("The scheduler was not started."));
        }

        //[Test]
        public void Stop_before_start_throws_exception()
        {
            Exception ex = null;
            try
            {
                scheduler.Stop();
            }
            catch (Exception e)
            {
                ex = e;
            }

            Assert.That(ex, Is.Not.Null);
            Assert.That(ex.GetType(), Is.EqualTo(typeof(InvalidOperationException)));
            Assert.That(ex.Message, Is.EqualTo("The scheduler was not started."));
        }

        [Test]
        public void Raises_on_stop_stopped_from_action()
        {
            scheduler.Post(() => scheduler.Stop());

            RunScheduler();

            Assert.That(schedulerDelegate.GotOnStopped, Is.True);
        }

        // XXX disabled
        //[Test]
        public void Raises_events_stopped_from_action_posted_before_start()
        {
            scheduler.Post(() => scheduler.Stop());

            RunScheduler();

            Assert.That(schedulerDelegate.GotOnStopped, Is.True);
        }

        // XXX disabled
        //[Test]
        public void Actions_posted_before_start_are_executed()
        {
            bool executed1 = false, executed2 = false;

            scheduler.Post(() => executed1 = true);
            scheduler.Post(() => executed2 = true);
            scheduler.Post(() => scheduler.Stop());

            RunScheduler();

            Assert.That(executed1);
            Assert.That(executed2);
            Assert.That(schedulerDelegate.GotOnStopped, Is.True);
        }

        [Test]
        public void Actions_posted_after_start_are_executed()
        {
            bool executed1 = false, executed2 = false;

            scheduler.Post(() =>
            {
                scheduler.Post(() => executed1 = true);
                scheduler.Post(() => executed2 = true);
                scheduler.Post(() => scheduler.Stop());
            });

            RunScheduler();

            Assert.That(executed1);
            Assert.That(executed2);
            Assert.That(schedulerDelegate.GotOnStopped, Is.True);
        }

        [Test]
        public void Actions_posted_from_actions_are_executed()
        {
            bool executed1 = false, executed2 = false;
            scheduler.Post(() =>
            {
                executed1 = true;
                scheduler.Post(() =>
                {
                    executed2 = true;
                    scheduler.Post(() => scheduler.Stop());
                });
            });

            RunScheduler();

            Assert.That(executed1);
            Assert.That(executed2);
            Assert.That(schedulerDelegate.GotOnStopped, Is.True);
        }

        [Test]
        public void Actions_posted_after_stop_are_not_executed()
        {
            bool executed1 = false, executed2 = false;

            scheduler.Post(() =>
            {
                executed1 = true;
                scheduler.Stop();
                scheduler.Post(() => executed2 = true);
            });

            RunScheduler();

            Assert.That(executed1);
            Assert.That(!executed2);
            Assert.That(schedulerDelegate.GotOnStopped, Is.True);
        }
    }
}
