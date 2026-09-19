using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using NUnit.Framework;

namespace GameSDK
{
    public sealed partial class StateMachineTests
    {
        [Test]
        public void ReservedNoStateCannotBeConstructedAndContextStartsEmpty()
        {
            Assert.That(StateMachine.NoState, Is.EqualTo(-1));
            ArgumentOutOfRangeException failure =
                Assert.Throws<ArgumentOutOfRangeException>(() => new ProbeState(StateMachine.NoState));
            Assert.That(failure.ParamName, Is.EqualTo("state"));
            var state = new ProbeState(0);
            AssertCleared(state);
        }

        [TestCase(0)]
        [TestCase(-2)]
        [TestCase(int.MinValue)]
        [TestCase(int.MaxValue)]
        public void NonReservedIdentifiersArePassedUnchangedThroughContext(int state)
        {
            var first = new ProbeState(state);
            var second = new ProbeState(1);
            StateMachine machine = Start(first, second);
            Assert.That(first.State, Is.EqualTo(state));
            Assert.That(first.FromValue, Is.EqualTo(StateMachine.NoState));
            machine.ChangeState(1);
            Assert.That(first.ToValue, Is.EqualTo(1));
            Assert.That(second.FromValue, Is.EqualTo(state));
            machine.ChangeState(state);
            Assert.That(second.ToValue, Is.EqualTo(state));
            Assert.That(first.FromValue, Is.EqualTo(1));
            machine.OnExit();
            Assert.That(first.ToValue, Is.EqualTo(StateMachine.NoState));
            machine.OnRelease();
        }

        [Test]
        public void RegistrationRejectsNullDuplicateIdsInstancesAndAnotherOwner()
        {
            var machine = new StateMachine();
            var other = new StateMachine();
            var first = new ProbeState(1);
            var duplicate = new ProbeState(1);
            Assert.Throws<ArgumentNullException>(() => machine.AddState(null));
            machine.AddState(first);
            Assert.That(first.Owner, Is.SameAs(machine));
            Assert.Throws<InvalidOperationException>(() => machine.AddState(first));
            Assert.Throws<InvalidOperationException>(() => machine.AddState(duplicate));
            Assert.Throws<InvalidOperationException>(() => other.AddState(first));
            Assert.That(duplicate.Owner, Is.Null);
            other.AddState(duplicate);
            machine.OnRelease();
            other.OnRelease();
        }

        [Test]
        public void StatesCanBeAddedBeforeAndAfterResetOrWhileAnotherStateIsCurrent()
        {
            var first = new ProbeState(1);
            var second = new ProbeState(2);
            var third = new ProbeState(3);
            StateMachine machine = Create(first);
            machine.Init();
            machine.AddState(second);
            machine.ChangeState(1);
            machine.AddState(third);
            Assert.That(machine.Current, Is.SameAs(first));
            Assert.That(first.ExitCount, Is.Zero);
            machine.Init();
            machine.AddState(new ProbeState(4));
            machine.ChangeState(3);
            Assert.That(machine.Current, Is.SameAs(third));
            machine.OnRelease();
        }

        [Test]
        public void SelectionRequiresOnlyRegistrationAndInvalidTargetDoesNotChangeCurrent()
        {
            var state = new ProbeState(1);
            StateMachine machine = Create(state);
            Assert.Throws<ArgumentException>(() => machine.ChangeState(2));
            Assert.That(state.EnterCount, Is.Zero);
            machine.ChangeState(1);
            Assert.Throws<ArgumentException>(() => machine.ChangeState(StateMachine.NoState));
            Assert.Throws<ArgumentException>(() => machine.ChangeState(404));
            Assert.That(machine.Current, Is.SameAs(state));
            Assert.That(state.EnterCount, Is.EqualTo(1));
            Assert.That(state.ExitCount + state.ExecuteCount + state.ReleaseCount, Is.Zero);
            machine.OnExecute(0f);
            Assert.That(state.ExecuteCount, Is.EqualTo(1));
            machine.OnRelease();
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ExitAndInitResetCurrentRetainRegistrationAndAllowImmediateReselection(bool useInit)
        {
            var state = new ProbeState(1);
            StateMachine machine = Create(state);
            machine.Init();
            machine.Init();
            Assert.That(state.EnterCount, Is.Zero);
            machine.ChangeState(1, new object());
            if (useInit) machine.Init(); else machine.OnExit();
            if (useInit) machine.Init(); else machine.OnExit();
            Assert.That(machine.Current, Is.Null);
            Assert.That(state.ExitCount, Is.EqualTo(1));
            Assert.That(state.LastTo, Is.EqualTo(StateMachine.NoState));
            Assert.That(state.DataValue, Is.Null);
            Assert.That(state.Owner, Is.SameAs(machine));
            machine.ChangeState(1);
            Assert.That(state.EnterCount, Is.EqualTo(2));
            Assert.That(state.FromValue, Is.EqualTo(StateMachine.NoState));
            machine.OnRelease();
        }

        [TestCase(-1f)]
        [TestCase(float.MinValue)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        public void ExecuteRejectsInvalidTimeBeforeCallingOrChangingState(float time)
        {
            var state = new ProbeState(1);
            StateMachine machine = Create(state);
            Assert.Throws<ArgumentOutOfRangeException>(() => machine.OnExecute(time));
            machine.ChangeState(1);
            Assert.Throws<ArgumentOutOfRangeException>(() => machine.OnExecute(time));
            Assert.That(machine.Current, Is.SameAs(state));
            Assert.That(state.ExecuteCount, Is.Zero);
            machine.OnExecute(0f);
            Assert.That(state.ExecuteCount, Is.EqualTo(1));
            machine.OnRelease();
        }

        [Test]
        public void FirstEntryReceivesContextAndSameStatePreservesItWithoutCallbacks()
        {
            var state = new ProbeState(1);
            StateMachine machine = Create(state);
            var data = new object();
            machine.ChangeState(1, data);
            machine.ChangeState(1, new object());
            Assert.That(state.EnterCount, Is.EqualTo(1));
            Assert.That(state.FromValue, Is.EqualTo(StateMachine.NoState));
            Assert.That(state.ToValue, Is.EqualTo(StateMachine.NoState));
            Assert.That(state.DataValue, Is.SameAs(data));
            Assert.That(state.LastData, Is.SameAs(data));
            Assert.That(state.ExecuteCount + state.ExitCount, Is.Zero);
            machine.OnExecute(0.25f);
            Assert.That(state.ExecuteCount, Is.EqualTo(1));
            Assert.That(state.LastDeltaTime, Is.EqualTo(0.25f));
            machine.OnRelease();
        }

        [Test]
        public void EmptyMachineAndDefaultCallbacksNeedNoLifecycleGuarding()
        {
            var machine = new StateMachine();
            machine.Init();
            machine.OnExecute(0f);
            machine.OnExecute(float.MaxValue);
            machine.OnExit();
            machine.OnRelease();
            machine.OnRelease();
            var state = new DefaultState(7);
            machine.AddState(state);
            machine.ChangeState(7);
            machine.OnExecute(1f);
            machine.OnExit();
            machine.OnRelease();
            Assert.That(state.State, Is.EqualTo(7));
            Assert.That(machine.Current, Is.Null);
        }

        [Test]
        public void ReleaseClearsAllContextsAndAllowsMachineAndStateReuse()
        {
            var first = new ProbeState(1);
            var second = new ProbeState(2);
            var neverEntered = new ProbeState(3);
            StateMachine machine = Create(first, second, neverEntered);
            machine.ChangeState(1, new object());
            machine.ChangeState(2, new object());
            machine.OnRelease();
            machine.OnRelease();
            Assert.That(machine.Current, Is.Null);
            Assert.That(first.ReleaseCount, Is.EqualTo(1));
            Assert.That(second.ReleaseCount, Is.EqualTo(1));
            Assert.That(neverEntered.ReleaseCount, Is.EqualTo(1));
            Assert.That(neverEntered.ExitCount, Is.Zero);
            AssertCleared(first);
            AssertCleared(second);
            AssertCleared(neverEntered);
            Assert.Throws<ArgumentException>(() => machine.ChangeState(1));

            machine.AddState(first);
            machine.ChangeState(1);
            Assert.That(first.FromValue, Is.EqualTo(StateMachine.NoState));
            var other = new StateMachine();
            other.AddState(second);
            other.ChangeState(2);
            Assert.That(second.Owner, Is.SameAs(other));
            Assert.That(second.FromValue, Is.EqualTo(StateMachine.NoState));
            machine.OnRelease();
            other.OnRelease();
            Assert.That(first.ReleaseCount, Is.EqualTo(2));
            Assert.That(second.ReleaseCount, Is.EqualTo(2));
        }

        private static StateMachine Create(params ProbeState[] states)
        {
            var machine = new StateMachine();
            foreach (ProbeState state in states) machine.AddState(state);
            return machine;
        }

        private static StateMachine Start(params ProbeState[] states)
        {
            StateMachine machine = Create(states);
            machine.ChangeState(states[0].State);
            return machine;
        }

        private static void AssertCleared(ProbeState state)
        {
            Assert.That(state.Owner, Is.Null);
            Assert.That(state.FromValue, Is.EqualTo(StateMachine.NoState));
            Assert.That(state.ToValue, Is.EqualTo(StateMachine.NoState));
            Assert.That(state.DataValue, Is.Null);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ThrowFromCallback(Exception failure)
        {
            throw failure;
        }

        private sealed class CallbackFailure : Exception
        {
            public CallbackFailure(string message) : base(message) { }
        }

        private sealed class DefaultState : StateBase
        {
            public DefaultState(int state) : base(state) { }
        }

        private sealed class ProbeState : StateBase
        {
            private readonly List<string> events;
            public Action EnterAction;
            public Action<float> ExecuteAction;
            public Action ExitAction;
            public Action ReleaseAction;
            public int EnterCount;
            public int ExecuteCount;
            public int ExitCount;
            public int ReleaseCount;
            public int LastFrom;
            public int LastTo;
            public object LastData;
            public float LastDeltaTime;
            public StateMachine Owner => Machine;
            public int FromValue => From;
            public int ToValue => To;
            public object DataValue => Data;

            public ProbeState(int state, List<string> events = null) : base(state)
            {
                this.events = events;
            }

            protected override void OnEnter()
            {
                EnterCount++;
                LastFrom = From;
                LastData = Data;
                Record("enter");
                EnterAction?.Invoke();
            }

            protected override void OnExecute(float deltaTime)
            {
                ExecuteCount++;
                LastDeltaTime = deltaTime;
                Record("execute");
                ExecuteAction?.Invoke(deltaTime);
            }

            protected override void OnExit()
            {
                ExitCount++;
                LastTo = To;
                Record("exit");
                ExitAction?.Invoke();
            }

            protected override void OnRelease()
            {
                ReleaseCount++;
                Record("release");
                ReleaseAction?.Invoke();
            }

            private void Record(string callback)
            {
                if (events != null)
                {
                    string current = Machine?.Current == null ? "null" : Machine.Current.State.ToString();
                    events.Add(State + "." + callback + " current=" + current);
                }
            }
        }
    }
}
