using System.Collections.Generic;
using NUnit.Framework;

namespace GameSDK.Tests
{
    public sealed partial class StateMachineTests
    {
        [Test]
        public void TransitionExitsOldReferenceThenSetsNewContextAndEntersNewReference()
        {
            var events = new List<string>();
            var first = new ProbeState(1, events);
            var second = new ProbeState(2, events);
            StateMachine machine = Create(first, second);
            var firstData = new object();
            var secondData = new object();
            machine.ChangeState(1, firstData);
            first.ExitAction = () =>
            {
                Assert.That(machine.Current, Is.SameAs(first));
                Assert.That(first.FromValue, Is.EqualTo(StateMachine.NoState));
                Assert.That(first.ToValue, Is.EqualTo(2));
                Assert.That(first.DataValue, Is.SameAs(firstData));
                Assert.That(second.EnterCount, Is.Zero);
            };
            second.EnterAction = () =>
            {
                Assert.That(machine.Current, Is.SameAs(second));
                Assert.That(second.FromValue, Is.EqualTo(1));
                Assert.That(second.ToValue, Is.EqualTo(StateMachine.NoState));
                Assert.That(second.DataValue, Is.SameAs(secondData));
                Assert.That(first.DataValue, Is.Null);
            };
            machine.ChangeState(2, secondData);

            CollectionAssert.AreEqual(new[]
            {
                "1.enter current=1", "1.exit current=1", "2.enter current=2"
            }, events);
            Assert.That(first.ToValue, Is.EqualTo(2));
            Assert.That(first.FromValue, Is.EqualTo(StateMachine.NoState));
            Assert.That(first.ExecuteCount + second.ExecuteCount, Is.Zero);
            machine.OnRelease();
        }

        [Test]
        public void ReentryReplacesPreviousContextAndResetsTargetBeforeCallback()
        {
            var first = new ProbeState(1);
            var second = new ProbeState(2);
            StateMachine machine = Start(first, second);
            machine.ChangeState(2);
            Assert.That(first.ToValue, Is.EqualTo(2));
            var data = new object();
            first.EnterAction = () =>
            {
                Assert.That(first.FromValue, Is.EqualTo(2));
                Assert.That(first.ToValue, Is.EqualTo(StateMachine.NoState));
                Assert.That(first.DataValue, Is.SameAs(data));
            };
            machine.ChangeState(1, data);
            machine.ChangeState(1, new object());
            Assert.That(first.EnterCount, Is.EqualTo(2));
            Assert.That(first.FromValue, Is.EqualTo(2));
            Assert.That(first.ToValue, Is.EqualTo(StateMachine.NoState));
            Assert.That(first.DataValue, Is.SameAs(data));
            machine.OnRelease();
        }

        [Test]
        public void UpdateSwitchesImmediatelyAndDoesNotDriveTheNewState()
        {
            var events = new List<string>();
            var first = new ProbeState(1, events);
            var second = new ProbeState(2, events);
            StateMachine machine = Start(first, second);
            var data = new object();
            first.ExecuteAction = time =>
            {
                machine.ChangeState(2, data);
                // Querying after return proves that the operation completed synchronously.
                Assert.That(machine.Current, Is.SameAs(second));
                Assert.That(second.EnterCount, Is.EqualTo(1));
                Assert.That(first.ExitCount, Is.EqualTo(1));
                events.Add("change returned");
            };
            machine.OnExecute(0.5f);

            CollectionAssert.AreEqual(new[]
            {
                "1.enter current=1", "1.execute current=1", "1.exit current=1",
                "2.enter current=2", "change returned"
            }, events);
            Assert.That(second.DataValue, Is.SameAs(data));
            Assert.That(first.ExecuteCount, Is.EqualTo(1));
            Assert.That(first.LastDeltaTime, Is.EqualTo(0.5f));
            Assert.That(second.ExecuteCount, Is.Zero);
            machine.OnExecute(0.25f);
            Assert.That(second.ExecuteCount, Is.EqualTo(1));
            machine.OnRelease();
        }

        [TestCase(false)]
        [TestCase(true)]
        public void UpdateExitOrReleaseCompletesBeforeTheCallReturns(bool release)
        {
            var first = new ProbeState(1);
            var second = new ProbeState(2);
            StateMachine machine = Start(first, second);
            first.ExecuteAction = time =>
            {
                if (release) machine.OnRelease(); else machine.OnExit();
                Assert.That(machine.Current, Is.Null);
                Assert.That(first.ExitCount, Is.EqualTo(1));
                Assert.That(first.LastTo, Is.EqualTo(StateMachine.NoState));
                Assert.That(first.ReleaseCount, Is.EqualTo(release ? 1 : 0));
                Assert.That(second.ReleaseCount, Is.EqualTo(release ? 1 : 0));
            };
            machine.OnExecute(0f);
            Assert.That(second.EnterCount + second.ExecuteCount, Is.Zero);
            Assert.That(first.ExecuteCount, Is.EqualTo(1));
            if (release)
            {
                AssertCleared(first);
                AssertCleared(second);
            }
            else
            {
                machine.ChangeState(2);
                Assert.That(machine.Current, Is.SameAs(second));
            }
            machine.OnRelease();
        }

        [Test]
        public void OneUpdateCanIssueOneHundredSameStateCallsWithoutChangingContext()
        {
            var state = new ProbeState(1);
            StateMachine machine = Create(state);
            var data = new object();
            machine.ChangeState(1, data);
            int completed = 0;
            state.ExecuteAction = time =>
            {
                for (int i = 0; i < 100; i++)
                {
                    machine.ChangeState(1, new object());
                    completed++;
                }
            };
            machine.OnExecute(0f);
            Assert.That(completed, Is.EqualTo(100));
            Assert.That(machine.Current, Is.SameAs(state));
            Assert.That(state.DataValue, Is.SameAs(data));
            Assert.That(state.EnterCount, Is.EqualTo(1));
            Assert.That(state.ExitCount, Is.Zero);
            machine.OnRelease();
        }
    }
}
