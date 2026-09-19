using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace GameSDK.Tests
{
    public sealed partial class StateMachineTests
    {
        [Test]
        public void TransitionExitFailureClearsCurrentAndDataWithoutEnteringTargetOrLockingMachine()
        {
            var first = new ProbeState(1);
            var second = new ProbeState(2);
            StateMachine machine = Create(first, second);
            var data = new object();
            machine.ChangeState(1, data);
            var failure = new CallbackFailure("exit");
            first.ExitAction = () =>
            {
                Assert.That(machine.Current, Is.SameAs(first));
                Assert.That(first.DataValue, Is.SameAs(data));
                Assert.That(first.ToValue, Is.EqualTo(2));
                ThrowFromCallback(failure);
            };
            CallbackFailure actual = Assert.Throws<CallbackFailure>(() => machine.ChangeState(2));
            AssertOriginalFailure(actual, failure);
            Assert.That(machine.Current, Is.Null);
            Assert.That(first.DataValue, Is.Null);
            Assert.That(first.ToValue, Is.EqualTo(2));
            Assert.That(second.EnterCount, Is.Zero);
            machine.ChangeState(2);
            Assert.That(second.FromValue, Is.EqualTo(StateMachine.NoState));
            Assert.That(machine.Current, Is.SameAs(second));
            machine.OnRelease();
            Assert.That(first.ExitCount, Is.EqualTo(1));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ExitOrInitFailureClearsCurrentAndDataAndCanBeFollowedByAnotherSelection(bool useInit)
        {
            var state = new ProbeState(1);
            StateMachine machine = Create(state);
            machine.ChangeState(1, new object());
            var failure = new CallbackFailure("reset exit");
            state.ExitAction = () => ThrowFromCallback(failure);
            CallbackFailure actual = useInit
                ? Assert.Throws<CallbackFailure>(() => machine.Init())
                : Assert.Throws<CallbackFailure>(() => machine.OnExit());
            AssertOriginalFailure(actual, failure);
            Assert.That(machine.Current, Is.Null);
            Assert.That(state.DataValue, Is.Null);
            Assert.That(state.ToValue, Is.EqualTo(StateMachine.NoState));
            machine.OnExit();
            Assert.That(state.ExitCount, Is.EqualTo(1));
            state.ExitAction = null;
            machine.ChangeState(1);
            Assert.That(state.EnterCount, Is.EqualTo(2));
            Assert.That(state.FromValue, Is.EqualTo(StateMachine.NoState));
            machine.OnRelease();
        }

        [TestCase(false)]
        [TestCase(true)]
        public void EnterFailureImmediatelyExitsPartialEntryWithoutRestoringPreviousState(bool hasPrevious)
        {
            var first = new ProbeState(1);
            var failing = new ProbeState(2);
            var next = new ProbeState(3);
            StateMachine machine = Create(first, failing, next);
            if (hasPrevious) machine.ChangeState(1, new object());
            var failure = new CallbackFailure("enter");
            var data = new object();
            failing.EnterAction = () => ThrowFromCallback(failure);
            failing.ExitAction = () =>
            {
                Assert.That(machine.Current, Is.SameAs(failing));
                Assert.That(failing.FromValue, Is.EqualTo(hasPrevious ? 1 : StateMachine.NoState));
                Assert.That(failing.ToValue, Is.EqualTo(StateMachine.NoState));
                Assert.That(failing.DataValue, Is.SameAs(data));
            };
            CallbackFailure actual = Assert.Throws<CallbackFailure>(() => machine.ChangeState(2, data));
            AssertOriginalFailure(actual, failure);
            Assert.That(machine.Current, Is.Null);
            Assert.That(failing.EnterCount, Is.EqualTo(1));
            Assert.That(failing.ExitCount, Is.EqualTo(1));
            Assert.That(failing.DataValue, Is.Null);
            Assert.That(first.DataValue, Is.Null);
            Assert.That(first.ExitCount, Is.EqualTo(hasPrevious ? 1 : 0));
            Assert.That(first.EnterCount, Is.EqualTo(hasPrevious ? 1 : 0));
            machine.ChangeState(3);
            Assert.That(next.FromValue, Is.EqualTo(StateMachine.NoState));
            machine.OnRelease();
            Assert.That(failing.ExitCount, Is.EqualTo(1));
            Assert.That(failing.ReleaseCount, Is.EqualTo(1));
        }

        [Test]
        public void EnterAndCleanupFailuresAreReportedInOrderWithOriginalStacks()
        {
            var state = new ProbeState(1);
            StateMachine machine = Create(state);
            var enterFailure = new CallbackFailure("enter");
            var exitFailure = new CallbackFailure("partial-entry exit");
            state.EnterAction = () => ThrowFromCallback(enterFailure);
            state.ExitAction = () => ThrowFromCallback(exitFailure);
            AggregateException actual =
                Assert.Throws<AggregateException>(() => machine.ChangeState(1, new object()));
            CollectionAssert.AreEqual(new Exception[] { enterFailure, exitFailure }, actual.InnerExceptions);
            AssertOriginalFailure(actual.InnerExceptions[0], enterFailure);
            AssertOriginalFailure(actual.InnerExceptions[1], exitFailure);
            Assert.That(machine.Current, Is.Null);
            Assert.That(state.DataValue, Is.Null);
            Assert.That(state.ToValue, Is.EqualTo(StateMachine.NoState));
            Assert.That(state.ExitCount, Is.EqualTo(1));
            machine.OnRelease();
            Assert.That(state.ExitCount, Is.EqualTo(1));
            AssertCleared(state);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ExecuteFailureKeepsTheStateCurrentAtThrowAndAllowsContinuedDriving(bool switchBeforeThrow)
        {
            var first = new ProbeState(1);
            var second = new ProbeState(2);
            StateMachine machine = Create(first, second);
            var firstData = new object();
            var secondData = new object();
            machine.ChangeState(1, firstData);
            var failure = new CallbackFailure("execute");
            first.ExecuteAction = time =>
            {
                if (switchBeforeThrow) machine.ChangeState(2, secondData);
                // An exception may escape after a completed switch; it must not roll it back.
                ThrowFromCallback(failure);
            };
            CallbackFailure actual = Assert.Throws<CallbackFailure>(() => machine.OnExecute(0.5f));
            AssertOriginalFailure(actual, failure);
            ProbeState current = switchBeforeThrow ? second : first;
            Assert.That(machine.Current, Is.SameAs(current));
            Assert.That(current.DataValue, Is.SameAs(switchBeforeThrow ? secondData : firstData));
            Assert.That(second.ExecuteCount, Is.Zero);
            Assert.That(first.ExitCount, Is.EqualTo(switchBeforeThrow ? 1 : 0));
            first.ExecuteAction = null;
            machine.OnExecute(0f);
            Assert.That(current.ExecuteCount, Is.EqualTo(switchBeforeThrow ? 1 : 2));
            machine.OnRelease();
        }

        [Test]
        public void ReleaseContinuesAfterAllErrorsAndUnbindsOnlyAfterEveryCallback()
        {
            var released = new List<int>();
            var first = new ProbeState(1);
            var second = new ProbeState(2);
            var third = new ProbeState(3);
            StateMachine machine = Create(first, second, third);
            machine.ChangeState(1, new object());
            var exitFailure = new CallbackFailure("exit");
            var firstReleaseFailure = new CallbackFailure("release first");
            var secondReleaseFailure = new CallbackFailure("release second");
            first.ExitAction = () => ThrowFromCallback(exitFailure);
            Action checkOwners = () =>
            {
                Assert.That(first.Owner, Is.SameAs(machine));
                Assert.That(second.Owner, Is.SameAs(machine));
                Assert.That(third.Owner, Is.SameAs(machine));
                Assert.That(machine.Current, Is.Null);
                Assert.That(first.DataValue, Is.Null);
            };
            first.ReleaseAction = () =>
            {
                checkOwners();
                released.Add(1);
                ThrowFromCallback(firstReleaseFailure);
            };
            second.ReleaseAction = () =>
            {
                checkOwners();
                released.Add(2);
                ThrowFromCallback(secondReleaseFailure);
            };
            third.ReleaseAction = () =>
            {
                checkOwners();
                released.Add(3);
            };
            AggregateException aggregate = Assert.Throws<AggregateException>(() => machine.OnRelease());
            CollectionAssert.AreEquivalent(new Exception[]
            {
                exitFailure, firstReleaseFailure, secondReleaseFailure
            }, aggregate.InnerExceptions);
            foreach (Exception error in aggregate.InnerExceptions)
                StringAssert.Contains(nameof(ThrowFromCallback), error.StackTrace);
            CollectionAssert.AreEquivalent(new[] { 1, 2, 3 }, released);
            Assert.That(first.ExitCount, Is.EqualTo(1));
            Assert.That(second.ExitCount + third.ExitCount, Is.Zero);
            AssertCleared(first);
            AssertCleared(second);
            AssertCleared(third);
            machine.OnRelease();
            Assert.That(first.ReleaseCount + second.ReleaseCount + third.ReleaseCount, Is.EqualTo(3));

            first.ExitAction = null;
            first.ReleaseAction = null;
            machine.AddState(first);
            machine.ChangeState(1);
            Assert.That(machine.Current, Is.SameAs(first));
            machine.OnRelease();
        }

        [Test]
        public void SingleReleaseFailureKeepsOriginalStackAndStillReleasesUnenteredStates()
        {
            var first = new ProbeState(1);
            var second = new ProbeState(2);
            StateMachine machine = Create(first, second);
            var failure = new CallbackFailure("release");
            first.ReleaseAction = () => ThrowFromCallback(failure);
            CallbackFailure actual = Assert.Throws<CallbackFailure>(() => machine.OnRelease());
            AssertOriginalFailure(actual, failure);
            Assert.That(first.ReleaseCount, Is.EqualTo(1));
            Assert.That(second.ReleaseCount, Is.EqualTo(1));
            Assert.That(first.ExitCount + second.ExitCount, Is.Zero);
            AssertCleared(first);
            AssertCleared(second);
            machine.OnRelease();
        }

        private static void AssertOriginalFailure(Exception actual, Exception expected)
        {
            Assert.That(actual, Is.SameAs(expected));
            StringAssert.Contains(nameof(ThrowFromCallback), actual.StackTrace);
        }
    }
}
