using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Kayak;
using NUnit.Framework;
using System.Threading;

namespace KayakTests.Net
{
    // TODO
    // - exceptions thrown on scheduler are handled in sane/predictable way.
    class SchedulerTests
    {
        ManualResetEventSlim wh;
        IScheduler scheduler;
        int NumOnStoppedEvents;
        int NumOnStartedEvents;

        void scheduler_OnStopped(object sender, EventArgs e)
        {
            NumOnStoppedEvents++;
            wh.Set();
        }

        void scheduler_OnStarted(object sender, EventArgs e)
        {
            NumOnStartedEvents++;
        }

        void StartAndWaitForStop()
        {
            scheduler.Start();
            WaitForStop();
        }

        void WaitForStop()
        {
            wh.Wait(TimeSpan.FromSeconds(5));
            wh.Reset();
        }

        [SetUp]
        public void SetUp()
        {
            NumOnStartedEvents = 0;
            NumOnStoppedEvents = 0;
            wh = new ManualResetEventSlim();
            scheduler = new KayakScheduler();
            scheduler.OnStarted += new EventHandler(scheduler_OnStarted);
            scheduler.OnStopped += new EventHandler(scheduler_OnStopped);
        }

        [TearDown]
        public void TearDown()
        {
            wh.Dispose();
            scheduler.OnStarted -= new EventHandler(scheduler_OnStarted);
            scheduler.OnStopped -= new EventHandler(scheduler_OnStopped);
        }

        [Test]
        public void Double_start_throws_exception()
        {
            scheduler.Start();

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

        [Test]
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

        [Test]
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
        public void Raises_events()
        {
            scheduler.Start();
            scheduler.Stop();

            WaitForStop();

            Assert.That(NumOnStartedEvents, Is.EqualTo(1));
            Assert.That(NumOnStoppedEvents, Is.EqualTo(1));
        }

        [Test]
        public void Raises_events_stopped_from_action()
        {
            scheduler.Start();
            scheduler.Post(() => scheduler.Stop());

            WaitForStop();

            Assert.That(NumOnStartedEvents, Is.EqualTo(1));
            Assert.That(NumOnStoppedEvents, Is.EqualTo(1));
        }
        [Test]
        public void Raises_events_stopped_from_action_posted_before_start()
        {
            scheduler.Post(() => scheduler.Stop());
            scheduler.Start();

            WaitForStop();

            Assert.That(NumOnStartedEvents, Is.EqualTo(1));
            Assert.That(NumOnStoppedEvents, Is.EqualTo(1));
        }

        [Test]
        public void Actions_posted_before_start_are_executed()
        {
            bool executed1 = false, executed2 = false;

            scheduler.Post(() => executed1 = true);
            scheduler.Post(() => executed2 = true);
            scheduler.Post(() => scheduler.Stop());

            scheduler.Start();
            
            WaitForStop();

            Assert.That(executed1);
            Assert.That(executed2);
            Assert.That(NumOnStartedEvents, Is.EqualTo(1));
            Assert.That(NumOnStoppedEvents, Is.EqualTo(1));
        }

        [Test]
        public void Actions_posted_after_start_are_executed()
        {
            bool executed1 = false, executed2 = false;

            scheduler.Start();

            scheduler.Post(() => executed1 = true);
            scheduler.Post(() => executed2 = true);
            scheduler.Post(() => scheduler.Stop());

            WaitForStop();

            Assert.That(executed1);
            Assert.That(executed2);
            Assert.That(NumOnStartedEvents, Is.EqualTo(1));
            Assert.That(NumOnStoppedEvents, Is.EqualTo(1));
        }

        [Test]
        public void Actions_posted_from_actions_are_executed()
        {
            bool executed1 = false, executed2 = false;
            scheduler.Start();
            scheduler.Post(() =>
            {
                executed1 = true;
                scheduler.Post(() => {
                    executed2 = true;
                    scheduler.Stop();
                });
            });

            WaitForStop();

            Assert.That(executed1);
            Assert.That(executed2);
            Assert.That(NumOnStartedEvents, Is.EqualTo(1));
            Assert.That(NumOnStoppedEvents, Is.EqualTo(1));
        }

        [Test]
        public void Actions_posted_after_stop_are_not_executed()
        {
            bool executed1 = false, executed2 = false;

            scheduler.Start();
            scheduler.Post(() => executed1 = true);
            scheduler.Stop();
            scheduler.Post(() => executed2 = true);

            WaitForStop();

            Assert.That(executed1);
            Assert.That(!executed2);
            Assert.That(NumOnStartedEvents, Is.EqualTo(1));
            Assert.That(NumOnStoppedEvents, Is.EqualTo(1));
        }

        [Test]
        public void Actions_posted_from_actions_after_stop_are_not_executed()
        {
            bool executed1 = false, executed2 = false;

            scheduler.Start();
            scheduler.Post(() =>
            {
                executed1 = true;
                scheduler.Stop();
                scheduler.Post(() => executed2 = true);
            });

            WaitForStop();

            Assert.That(executed1);
            Assert.That(!executed2);
            Assert.That(NumOnStartedEvents, Is.EqualTo(1));
            Assert.That(NumOnStoppedEvents, Is.EqualTo(1));
        }

    }
}
