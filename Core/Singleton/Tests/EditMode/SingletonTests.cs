using System;
using System.Reflection;
using System.Threading;
using NUnit.Framework;

namespace GameSDK.Tests
{
    [TestFixture]
    public sealed class SingletonTests
    {
        private static int invalidConstructorCalls;

        [SetUp]
        public void SetUp()
        {
            ClearInstances();
            Primary.ResetObservations();
            Secondary.ResetObservations();
            invalidConstructorCalls = 0;
        }

        [TearDown]
        public void TearDown()
        {
            ClearInstances();
        }

        [Test]
        public void Get_ReusesReadyInstance_AndKeepsTypesIndependent()
        {
            Primary first = Primary.Instance;
            Secondary other = Secondary.Instance;

            Assert.That(Primary.Instance, Is.SameAs(first));
            Assert.That(Secondary.Instance, Is.SameAs(other));
            Assert.That(first.InitializeCalls, Is.EqualTo(1));
            Assert.That(other.InitializeCalls, Is.EqualTo(1));
            Assert.That(first.Resource, Is.Not.SameAs(other.Resource));

            Primary.ReleaseInstance();

            Assert.That(first.Resource, Is.Null);
            Assert.That(other.Resource, Is.Not.Null);
            Assert.That(other.ReleaseCalls, Is.Zero);
            Assert.That(Secondary.Instance, Is.SameAs(other));
        }

        [Test]
        public void Release_WhenEmpty_DoesNotConstruct()
        {
            Primary.ReleaseInstance();
            Primary.ReleaseInstance();

            Assert.That(Primary.ConstructionCalls, Is.Zero);
            Assert.That(Primary.LastConstructed, Is.Null);
        }

        [Test]
        public void Release_Twice_CleansOnce_AndNextGetCreatesFreshResources()
        {
            Primary old = Primary.Instance;
            object oldResource = old.Resource;

            Primary.ReleaseInstance();
            Primary.ReleaseInstance();

            Assert.That(Primary.ConstructionCalls, Is.EqualTo(1));
            Assert.That(old.ReleaseCalls, Is.EqualTo(1));
            Assert.That(old.Resource, Is.Null);

            Primary replacement = Primary.Instance;

            Assert.That(replacement, Is.Not.SameAs(old));
            Assert.That(replacement.Resource, Is.Not.Null.And.Not.SameAs(oldResource));
            Assert.That(replacement.InitializeCalls, Is.EqualTo(1));
            Assert.That(replacement.ReleaseCalls, Is.Zero);
            Assert.That(Primary.Instance, Is.SameAs(replacement));
            Assert.That(old.Resource, Is.Null);
            Assert.That(old.ReleaseCalls, Is.EqualTo(1));
        }

        [TestCase(typeof(AbstractInvalid))]
        [TestCase(typeof(PublicConstructorInvalid))]
        [TestCase(typeof(ProtectedConstructorInvalid))]
        [TestCase(typeof(InternalConstructorInvalid))]
        [TestCase(typeof(MissingDefaultConstructorInvalid))]
        [TestCase(typeof(PublicOverloadInvalid))]
        public void ConstructorContract_InvalidTypesAreRejectedBeforeConstruction_AndRemainEmpty(Type type)
        {
            Type closedBase = typeof(Singleton<>).MakeGenericType(type);
            PropertyInfo instanceProperty = closedBase.GetProperty("Instance");
            MethodInfo releaseMethod = closedBase.GetMethod("ReleaseInstance");

            for (int attempt = 0; attempt < 2; attempt++)
            {
                TargetInvocationException wrapper = Assert.Throws<TargetInvocationException>(
                    () => instanceProperty.GetValue(null));
                Assert.That(wrapper.InnerException, Is.TypeOf<InvalidOperationException>());
                Assert.DoesNotThrow(() => releaseMethod.Invoke(null, null));
            }

            Assert.That(invalidConstructorCalls, Is.Zero);
        }

        [Test]
        public void ConstructorContract_ActualTypeMustMatchGenericType()
        {
            Assert.Throws<InvalidOperationException>(() => new WrongSelf());
            Assert.That(Primary.ConstructionCalls, Is.Zero);
            Assert.That(Primary.Instance, Is.TypeOf<Primary>());
        }

        [Test]
        public void ConstructorFailure_PreservesOriginalException_DoesNotRelease_AndCanRetry()
        {
            var original = new ApplicationException("constructor failed");
            Primary.ConstructionHook = value => { throw original; };

            ApplicationException observed = Assert.Throws<ApplicationException>(() => _ = Primary.Instance);
            Primary failed = Primary.LastConstructed;

            Assert.That(observed, Is.SameAs(original));
            Assert.That(failed.InitializeCalls, Is.Zero);
            Assert.That(failed.ReleaseCalls, Is.Zero);
            Assert.That(failed.Resource, Is.Null);
            Assert.DoesNotThrow(Primary.ReleaseInstance);

            Primary.ConstructionHook = null;
            Primary replacement = Primary.Instance;

            Assert.That(replacement, Is.Not.SameAs(failed));
            Assert.That(replacement.InitializeCalls, Is.EqualTo(1));
            Assert.That(Primary.ConstructionCalls, Is.EqualTo(2));
            Assert.That(failed.ReleaseCalls, Is.Zero);
        }

        [Test]
        public void InitializationFailure_CleansPartialResourceOnce_PreservesError_AndCanRetry()
        {
            var original = new ApplicationException("initialization failed");
            Primary.InitializeHook = value =>
            {
                Assert.That(value.Resource, Is.Not.Null);
                throw original;
            };

            ApplicationException observed = Assert.Throws<ApplicationException>(() => _ = Primary.Instance);
            Primary failed = Primary.LastConstructed;

            Assert.That(observed, Is.SameAs(original));
            Assert.That(failed.InitializeCalls, Is.EqualTo(1));
            Assert.That(failed.ReleaseCalls, Is.EqualTo(1));
            Assert.That(failed.Resource, Is.Null);
            Primary.ReleaseInstance();
            Assert.That(failed.ReleaseCalls, Is.EqualTo(1));

            Primary.InitializeHook = null;
            Primary replacement = Primary.Instance;

            Assert.That(replacement, Is.Not.SameAs(failed));
            Assert.That(replacement.Resource, Is.Not.Null);
            Assert.That(Primary.ConstructionCalls, Is.EqualTo(2));
        }

        [Test]
        public void InitializationAndCleanupFailure_PreservesBothErrorsInOrder_AndCanRetry()
        {
            var initializationError = new ApplicationException("initialization failed");
            var releaseError = new ApplicationException("cleanup failed");
            Primary.InitializeHook = value => { throw initializationError; };
            Primary.ReleaseHook = value => { throw releaseError; };

            AggregateException observed = Assert.Throws<AggregateException>(() => _ = Primary.Instance);
            Primary failed = Primary.LastConstructed;

            Assert.That(observed.InnerExceptions.Count, Is.EqualTo(2));
            Assert.That(observed.InnerExceptions[0], Is.SameAs(initializationError));
            Assert.That(observed.InnerExceptions[1], Is.SameAs(releaseError));
            Assert.That(failed.ReleaseCalls, Is.EqualTo(1));
            Assert.That(failed.Resource, Is.Null);
            Assert.DoesNotThrow(Primary.ReleaseInstance);
            Assert.That(failed.ReleaseCalls, Is.EqualTo(1));

            Primary.InitializeHook = null;
            Primary.ReleaseHook = null;
            Assert.That(Primary.Instance, Is.Not.SameAs(failed));
        }

        [Test]
        public void ReleaseFailure_PreservesOriginalError_FinishesRegistration_AndCanRetry()
        {
            Primary failed = Primary.Instance;
            var original = new ApplicationException("release failed");
            Primary.ReleaseHook = value => { throw original; };

            ApplicationException observed = Assert.Throws<ApplicationException>(Primary.ReleaseInstance);

            Assert.That(observed, Is.SameAs(original));
            Assert.That(failed.ReleaseCalls, Is.EqualTo(1));
            Assert.That(failed.Resource, Is.Null);
            Assert.DoesNotThrow(Primary.ReleaseInstance);
            Assert.That(failed.ReleaseCalls, Is.EqualTo(1));

            Primary.ReleaseHook = null;
            Primary replacement = Primary.Instance;
            Assert.That(replacement, Is.Not.SameAs(failed));
            Assert.That(replacement.Resource, Is.Not.Null);
        }

        [TestCase("Construction")]
        [TestCase("Initialization")]
        public void ConcurrentFirstGet_WaitsForCreation_AndReturnsTheSameReadyInstance(string phase)
        {
            Primary first = null;
            Primary waiting = null;
            ConcurrentOutcome outcome = RunWhileLifecycleBlocked(
                phase, () => first = Primary.Instance, () => waiting = Primary.Instance);

            Assert.That(outcome.OwnerError, Is.Null);
            Assert.That(outcome.WaiterError, Is.Null);
            Assert.That(waiting, Is.SameAs(first));
            Assert.That(first, Is.SameAs(Primary.Instance));
            Assert.That(Primary.ConstructionCalls, Is.EqualTo(1));
            Assert.That(first.InitializeCalls, Is.EqualTo(1));
            Assert.That(first.ReleaseCalls, Is.Zero);
            Assert.That(first.Resource, Is.Not.Null);
        }

        [Test]
        public void ReleaseDuringInitialization_WaitsThenCleansTheCompletedInstance()
        {
            Primary initialized = null;
            ConcurrentOutcome outcome = RunWhileLifecycleBlocked(
                "Initialization", () => initialized = Primary.Instance, Primary.ReleaseInstance);

            Assert.That(outcome.OwnerError, Is.Null);
            Assert.That(outcome.WaiterError, Is.Null);
            Assert.That(initialized.InitializeCalls, Is.EqualTo(1));
            Assert.That(initialized.ReleaseCalls, Is.EqualTo(1));
            Assert.That(initialized.Resource, Is.Null);
            Assert.That(Primary.ConstructionCalls, Is.EqualTo(1));
            Primary replacement = Primary.Instance;
            Assert.That(replacement, Is.Not.SameAs(initialized));
            Assert.That(replacement.Resource, Is.Not.Null);
            Assert.That(Primary.ConstructionCalls, Is.EqualTo(2));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void GetDuringRelease_WaitsThenCreatesANewInstanceEvenWhenReleaseFails(bool releaseFails)
        {
            Primary old = Primary.Instance;
            Primary waiting = null;
            var releaseError = new ApplicationException("blocked release failed");
            ConcurrentOutcome outcome = RunWhileLifecycleBlocked(
                "Release", Primary.ReleaseInstance, () => waiting = Primary.Instance,
                value =>
                {
                    if (releaseFails) throw releaseError;
                });

            Assert.That(outcome.OwnerError, Is.SameAs(releaseFails ? releaseError : null));
            Assert.That(outcome.WaiterError, Is.Null);
            Assert.That(old.ReleaseCalls, Is.EqualTo(1));
            Assert.That(old.Resource, Is.Null);
            Assert.That(waiting, Is.Not.SameAs(old));
            Assert.That(waiting, Is.SameAs(Primary.Instance));
            Assert.That(waiting.InitializeCalls, Is.EqualTo(1));
            Assert.That(waiting.ReleaseCalls, Is.Zero);
            Assert.That(waiting.Resource, Is.Not.Null);
            Assert.That(Primary.ConstructionCalls, Is.EqualTo(2));
        }

        [Test]
        public void ConcurrentRelease_WaitsThenDoesNothingAfterTheFirstReleaseClearsTheInstance()
        {
            Primary old = Primary.Instance;
            ConcurrentOutcome outcome = RunWhileLifecycleBlocked(
                "Release", Primary.ReleaseInstance, Primary.ReleaseInstance);

            Assert.That(outcome.OwnerError, Is.Null);
            Assert.That(outcome.WaiterError, Is.Null);
            Assert.That(old.ReleaseCalls, Is.EqualTo(1));
            Assert.That(old.Resource, Is.Null);
            Assert.That(Primary.ConstructionCalls, Is.EqualTo(1));
            Assert.That(Primary.LastConstructed, Is.SameAs(old));
        }

        [TestCase("Construction")]
        [TestCase("Initialization")]
        [TestCase("Cleanup")]
        public void WaitingGet_RetriesAfterTheCurrentCreationFails(string phase)
        {
            var original = new ApplicationException("blocked creation failed");
            int attempts = 0;
            Primary failed = null;
            Primary waiting = null;
            Action<Primary> failFirstAttempt = value =>
            {
                if (Interlocked.Increment(ref attempts) == 1)
                {
                    failed = value;
                    throw original;
                }
            };
            if (phase == "Cleanup") Primary.InitializeHook = failFirstAttempt;
            ConcurrentOutcome outcome = RunWhileLifecycleBlocked(
                phase == "Cleanup" ? "Release" : phase,
                () => _ = Primary.Instance, () => waiting = Primary.Instance,
                phase == "Cleanup" ? null : failFirstAttempt);

            Assert.That(outcome.OwnerError, Is.SameAs(original));
            Assert.That(outcome.WaiterError, Is.Null);
            Assert.That(failed.InitializeCalls, Is.EqualTo(phase == "Construction" ? 0 : 1));
            Assert.That(failed.ReleaseCalls, Is.EqualTo(phase == "Construction" ? 0 : 1));
            Assert.That(failed.Resource, Is.Null);
            Assert.That(waiting, Is.Not.SameAs(failed));
            Assert.That(waiting, Is.SameAs(Primary.Instance));
            Assert.That(waiting.InitializeCalls, Is.EqualTo(1));
            Assert.That(waiting.ReleaseCalls, Is.Zero);
            Assert.That(waiting.Resource, Is.Not.Null);
            Assert.That(Primary.ConstructionCalls, Is.EqualTo(2));
        }

        private static ConcurrentOutcome RunWhileLifecycleBlocked(
            string phase, Action ownerAction, Action waiterAction, Action<Primary> afterResume = null)
        {
            using (var entered = new ManualResetEventSlim(false))
            using (var resume = new ManualResetEventSlim(false))
            using (var waiterAttempting = new ManualResetEventSlim(false))
            using (var waiterCompleted = new ManualResetEventSlim(false))
            using (var otherTypeCompleted = new ManualResetEventSlim(false))
            {
                SetHook(phase, value =>
                {
                    entered.Set();
                    if (!resume.Wait(TimeSpan.FromSeconds(15)))
                    {
                        throw new TimeoutException("The test did not unblock the lifecycle hook.");
                    }
                    afterResume?.Invoke(value);
                });

                var outcome = new ConcurrentOutcome();
                Exception otherTypeError = null;
                Secondary other = null;
                var owner = new Thread(() => outcome.OwnerError = Capture(ownerAction))
                    { IsBackground = true };
                var waiter = new Thread(() =>
                {
                    waiterAttempting.Set();
                    try
                    {
                        outcome.WaiterError = Capture(waiterAction);
                    }
                    finally
                    {
                        waiterCompleted.Set();
                    }
                }) { IsBackground = true };
                var otherType = new Thread(() =>
                {
                    try
                    {
                        otherTypeError = Capture(() => other = Secondary.Instance);
                    }
                    finally
                    {
                        otherTypeCompleted.Set();
                    }
                }) { IsBackground = true };

                bool enteredInTime = false;
                bool waiterAttempted = false;
                bool waiterStayedPending = false;
                bool otherTypeFinishedWhileBlocked = false;
                bool waiterStarted = false;
                bool otherTypeStarted = false;
                bool ownerJoined;
                bool waiterJoined;
                bool otherTypeJoined;
                try
                {
                    owner.Start();
                    enteredInTime = entered.Wait(TimeSpan.FromSeconds(5));
                    if (enteredInTime)
                    {
                        waiter.Start();
                        waiterStarted = true;
                        waiterAttempted = waiterAttempting.Wait(TimeSpan.FromSeconds(5));
                        if (waiterAttempted)
                        {
                            waiterStayedPending = !waiterCompleted.Wait(TimeSpan.FromMilliseconds(150));
                        }
                        otherType.Start();
                        otherTypeStarted = true;
                        otherTypeFinishedWhileBlocked = otherTypeCompleted.Wait(TimeSpan.FromSeconds(5));
                        waiterStayedPending = waiterStayedPending && !waiterCompleted.IsSet;
                    }
                }
                finally
                {
                    resume.Set();
                    ownerJoined = owner.Join(TimeSpan.FromSeconds(5));
                    waiterJoined = !waiterStarted || waiter.Join(TimeSpan.FromSeconds(5));
                    otherTypeJoined = !otherTypeStarted || otherType.Join(TimeSpan.FromSeconds(5));
                    SetHook(phase, null);
                }

                Assert.That(ownerJoined, Is.True, "The lifecycle worker must terminate.");
                Assert.That(waiterJoined, Is.True, "The waiting worker must terminate.");
                Assert.That(otherTypeJoined, Is.True, "The other-type worker must terminate.");
                Assert.That(enteredInTime, Is.True, "The lifecycle hook must be reached.");
                Assert.That(waiterAttempted, Is.True, "The same-type worker must attempt its operation.");
                Assert.That(waiterStayedPending, Is.True,
                    "A same-type call must wait until the current lifecycle operation completes.");
                Assert.That(otherTypeFinishedWhileBlocked, Is.True,
                    "A different singleton type must remain usable while this type's hook is blocked.");
                Assert.That(otherTypeError, Is.Null);
                Assert.That(other, Is.Not.Null);
                Assert.That(other.InitializeCalls, Is.EqualTo(1));
                Assert.That(other.Resource, Is.Not.Null);
                Assert.That(other.ReleaseCalls, Is.Zero);
                return outcome;
            }
        }

        private sealed class ConcurrentOutcome
        {
            internal Exception OwnerError;
            internal Exception WaiterError;
        }

        private static Exception Capture(Action action)
        {
            try
            {
                action();
                return null;
            }
            catch (Exception error)
            {
                return error;
            }
        }

        private static void SetHook(string phase, Action<Primary> callback)
        {
            switch (phase)
            {
                case "Construction":
                    Primary.ConstructionHook = callback;
                    break;
                case "Initialization":
                    Primary.InitializeHook = callback;
                    break;
                case "Release":
                    Primary.ReleaseHook = callback;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(phase));
            }
        }

        private static void ClearInstances()
        {
            Primary.ClearHooks();
            Secondary.ClearHooks();
            Primary.ReleaseInstance();
            Secondary.ReleaseInstance();
        }

        private abstract class Probe<T> : Singleton<T> where T : Probe<T>
        {
            internal static Action<T> ConstructionHook;
            internal static Action<T> InitializeHook;
            internal static Action<T> ReleaseHook;
            internal static int ConstructionCalls;
            internal static T LastConstructed;

            internal int InitializeCalls;
            internal int ReleaseCalls;
            internal object Resource;

            protected Probe()
            {
                ConstructionCalls++;
                LastConstructed = (T)this;
                ConstructionHook?.Invoke((T)this);
            }

            protected override void OnInitialize()
            {
                InitializeCalls++;
                Resource = new object();
                InitializeHook?.Invoke((T)this);
            }

            protected override void OnRelease()
            {
                ReleaseCalls++;
                Resource = null;
                ReleaseHook?.Invoke((T)this);
            }

            internal static void ClearHooks()
            {
                ConstructionHook = null;
                InitializeHook = null;
                ReleaseHook = null;
            }

            internal static void ResetObservations()
            {
                ClearHooks();
                ConstructionCalls = 0;
                LastConstructed = null;
            }
        }

        private sealed class Primary : Probe<Primary>
        {
            private Primary() { }
        }

        private sealed class Secondary : Probe<Secondary>
        {
            private Secondary() { }
        }

        private sealed class WrongSelf : Singleton<Primary>
        {
            public WrongSelf() { }
        }

        private abstract class AbstractInvalid : Singleton<AbstractInvalid>
        {
            private AbstractInvalid() { invalidConstructorCalls++; }
        }

        private sealed class PublicConstructorInvalid : Singleton<PublicConstructorInvalid>
        {
            public PublicConstructorInvalid() { invalidConstructorCalls++; }
        }

        private class ProtectedConstructorInvalid : Singleton<ProtectedConstructorInvalid>
        {
            protected ProtectedConstructorInvalid() { invalidConstructorCalls++; }
        }

        private sealed class InternalConstructorInvalid : Singleton<InternalConstructorInvalid>
        {
            internal InternalConstructorInvalid() { invalidConstructorCalls++; }
        }

        private sealed class MissingDefaultConstructorInvalid : Singleton<MissingDefaultConstructorInvalid>
        {
            private MissingDefaultConstructorInvalid(int value) { invalidConstructorCalls++; }
        }

        private sealed class PublicOverloadInvalid : Singleton<PublicOverloadInvalid>
        {
            private PublicOverloadInvalid() { invalidConstructorCalls++; }
            public PublicOverloadInvalid(int value) { invalidConstructorCalls++; }
        }
    }
}
