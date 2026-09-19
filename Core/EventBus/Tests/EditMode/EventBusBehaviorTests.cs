using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace GameSDK
{
    [TestFixture]
    public sealed class EventBusBehaviorTests
    {
        private const string Key = "EventBus.Tests.Message";
        private EventBus bus;

        [SetUp]
        public void SetUp()
        {
            EventBus.ReleaseInstance();
            UtilsLog.SetEnabled(true);
            UtilsLog.SetLevel(LogLevel.Info);
            bus = EventBus.Instance;
        }

        [TearDown]
        public void TearDown()
        {
            EventBus.ReleaseInstance();
            UtilsLog.SetEnabled(true);
            UtilsLog.SetLevel(LogLevel.Info);
        }

        [Test]
        public void PublicContractHasTwelveVoidTypedOverloadsAndUsesRealSingleton()
        {
            Type type = typeof(EventBus);
            Assert.That(type.BaseType, Is.EqualTo(typeof(Singleton<EventBus>)));
            Assert.That(type.GetConstructors(), Is.Empty);
            Assert.That(type.GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic,
                null, Type.EmptyTypes, null).IsPrivate, Is.True);
            Assert.That(type.GetMethod(nameof(EventBus.ReleaseInstance),
                BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly), Is.Null);
            MethodInfo release = type.GetMethod(nameof(EventBus.ReleaseInstance),
                BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);
            Assert.That(release, Is.Not.Null);
            Assert.That(release.DeclaringType, Is.EqualTo(typeof(Singleton<EventBus>)));
            Assert.That(type.GetProperty(nameof(EventBus.Instance),
                BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly), Is.Null);
            PropertyInfo instance = type.GetProperty(nameof(EventBus.Instance),
                BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);
            Assert.That(instance, Is.Not.Null);
            Assert.That(instance.DeclaringType, Is.EqualTo(typeof(Singleton<EventBus>)));
            MethodInfo[] methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            Assert.That(methods.Length, Is.EqualTo(12));
            foreach (string name in new[] { "Subscribe", "Unsubscribe", "Publish" })
            {
                MethodInfo[] overloads = methods.Where(method => method.Name == name)
                    .OrderBy(method => method.GetGenericArguments().Length).ToArray();
                Assert.That(overloads.Length, Is.EqualTo(4));
                for (int arity = 0; arity < 4; arity++)
                {
                    MethodInfo method = overloads[arity];
                    Assert.That(method.ReturnType, Is.EqualTo(typeof(void)));
                    Assert.That(method.GetGenericArguments().Length, Is.EqualTo(arity));
                    ParameterInfo[] parameters = method.GetParameters();
                    Assert.That(parameters[0].ParameterType, Is.EqualTo(typeof(string)));
                    if (name == "Publish")
                    {
                        Assert.That(parameters.Length, Is.EqualTo(arity + 1));
                        for (int i = 0; i < arity; i++)
                            Assert.That(parameters[i + 1].ParameterType, Is.EqualTo(method.GetGenericArguments()[i]));
                    }
                    else
                    {
                        Assert.That(parameters.Length, Is.EqualTo(2));
                        Type expected = arity == 0 ? typeof(Action) :
                            new[] { typeof(Action<>), typeof(Action<,>), typeof(Action<,,>) }[arity - 1]
                                .MakeGenericType(method.GetGenericArguments());
                        Assert.That(parameters[1].ParameterType, Is.EqualTo(expected));
                    }
                }
            }
            Assert.That(EventBus.Instance, Is.SameAs(bus));
            Assert.That(Singleton<EventBus>.Instance, Is.SameAs(bus));
        }

        [TestCase(0), TestCase(1), TestCase(2), TestCase(3)]
        public void TypedListenersReceiveArgumentsInOrderAndUnsubscribeIndependently(int arity)
        {
            var c = new EventCase(bus, arity);
            var order = new List<int>();
            var first = new Listener { Effect = () => order.Add(1) };
            var second = new Listener { Effect = () => order.Add(2) };
            var otherKey = new Listener();
            c.Subscribe(Key, c.Callback(first));
            c.Subscribe(Key, c.Callback(second));
            c.Subscribe(Key.ToLowerInvariant(), c.Callback(otherKey));
            c.Publish(Key);
            Assert.That(order, Is.EqualTo(new[] { 1, 2 }));
            Assert.That(otherKey.Calls, Is.Zero, "Keys use ordinal, case-sensitive identity.");
            if (arity >= 1) Assert.That(first.Number, Is.EqualTo(42));
            if (arity >= 2) Assert.That(first.Text, Is.EqualTo("payload"));
            if (arity >= 3) Assert.That(first.Payload, Is.SameAs(c.Payload));
            c.Unsubscribe(Key, c.Callback(first));
            c.Publish(Key);
            Assert.That(first.Calls, Is.EqualTo(1));
            Assert.That(second.Calls, Is.EqualTo(2));
            c.Unsubscribe(Key, c.Callback(second));
            c.Publish(Key);
            Assert.That(second.Calls, Is.EqualTo(2));
        }

        [TestCase(0), TestCase(1), TestCase(2), TestCase(3)]
        public void InvalidInputsDoNotBindKeysOrChangeExistingListeners(int arity)
        {
            var c = new EventCase(bus, arity);
            var listener = new Listener();
            Delegate callback = c.Callback(listener);
            foreach (string badKey in new[] { null, "" })
            {
                Assert.Catch<ArgumentException>(() => c.Subscribe(badKey, callback));
                Assert.Catch<ArgumentException>(() => c.Unsubscribe(badKey, callback));
                Assert.Catch<ArgumentException>(() => c.Publish(badKey));
            }
            Assert.Throws<ArgumentNullException>(() => c.Subscribe(Key, null));
            Assert.Throws<ArgumentNullException>(() => c.Unsubscribe(Key, null));
            var different = new EventCase(bus, (arity + 1) % 4);
            var original = new Listener();
            different.Subscribe(Key, different.Callback(original));
            Assert.Throws<InvalidOperationException>(() => c.Subscribe(Key, callback));
            Assert.Throws<InvalidOperationException>(() => c.Unsubscribe(Key, callback));
            Assert.Throws<InvalidOperationException>(() => c.Publish(Key));
            different.Publish(Key);
            Assert.That(original.Calls, Is.EqualTo(1));
            c.Subscribe(Key + ".valid", callback);
            Assert.Throws<ArgumentNullException>(() => c.Subscribe(Key + ".valid", null));
            Assert.Throws<ArgumentNullException>(() => c.Unsubscribe(Key + ".valid", null));
            c.Publish(Key + ".valid");
            Assert.That(listener.Calls, Is.EqualTo(1));
            c.Unsubscribe(Key + ".valid", callback);
            Assert.Throws<ArgumentNullException>(() => c.Unsubscribe(Key + ".valid", null));
        }

        [TestCase(0), TestCase(1), TestCase(2), TestCase(3)]
        public void UnknownPublishAndUnsubscribeDoNotBindButSuccessfulSubscriptionDoes(int arity)
        {
            var c = new EventCase(bus, arity);
            var listener = new Listener();
            c.Publish(Key);
            c.Unsubscribe(Key, c.Callback(listener));
            var different = new EventCase(bus, (arity + 1) % 4);
            Delegate other = different.Callback(new Listener());
            different.Subscribe(Key, other);
            different.Unsubscribe(Key, other);
            Assert.Throws<InvalidOperationException>(() => c.Subscribe(Key, c.Callback(listener)));
            Assert.Throws<InvalidOperationException>(() => c.Unsubscribe(Key, c.Callback(listener)));
            Assert.Throws<InvalidOperationException>(() => c.Publish(Key));
            different.Subscribe(Key, other);
            Assert.DoesNotThrow(() => different.Publish(Key));
        }

        [TestCase(0), TestCase(1), TestCase(2), TestCase(3)]
        public void DelegateEqualityPairsReconstructedMethodsAndSeparatesTargets(int arity)
        {
            var c = new EventCase(bus, arity);
            var first = new Listener();
            var second = new Listener();
            Delegate original = c.Callback(first);
            Delegate reconstructed = c.Callback(first);
            Assert.That(ReferenceEquals(original, reconstructed), Is.False);
            Assert.That(original.Equals(reconstructed), Is.True);
            c.Subscribe(Key, original);
            Assert.Throws<InvalidOperationException>(() => c.Subscribe(Key, reconstructed));
            c.Subscribe(Key, c.Callback(second));
            c.Publish(Key);
            Assert.That(first.Calls, Is.EqualTo(1));
            Assert.That(second.Calls, Is.EqualTo(1));
            c.Unsubscribe(Key, reconstructed);
            c.Unsubscribe(Key, reconstructed);
            c.Publish(Key);
            Assert.That(first.Calls, Is.EqualTo(1));
            Assert.That(second.Calls, Is.EqualTo(2));
            c.Subscribe(Key, reconstructed);
            c.Publish(Key);
            Assert.That(first.Calls, Is.EqualTo(2));
        }

        [TestCase(0, false), TestCase(1, false), TestCase(2, false), TestCase(3, false)]
        [TestCase(0, true), TestCase(1, true), TestCase(2, true), TestCase(3, true)]
        public void CombinedCallbacksAreRejectedAtomicallyEvenWhenEntriesAreEqual(int arity, bool equalEntries)
        {
            var c = new EventCase(bus, arity);
            var first = new Listener();
            var second = new Listener();
            Delegate one = c.Callback(first);
            Delegate two = c.OtherCallback(second);
            Delegate combined = Delegate.Combine(one, equalEntries ? one : two);
            Assert.Throws<ArgumentException>(() => c.Subscribe(Key, combined));
            Assert.Throws<ArgumentException>(() => c.Unsubscribe(Key, combined));
            var different = new EventCase(bus, (arity + 1) % 4);
            different.Subscribe(Key, different.Callback(new Listener()));
            c.Subscribe(Key + ".bound", one);
            c.Subscribe(Key + ".bound", two);
            Assert.Throws<ArgumentException>(() => c.Subscribe(Key + ".bound", combined));
            Assert.Throws<ArgumentException>(() => c.Unsubscribe(Key + ".bound", combined));
            c.Publish(Key + ".bound");
            Assert.That(first.Calls, Is.EqualTo(1));
            Assert.That(second.Calls, Is.EqualTo(1));
        }

        [Test]
        public void GenericTypeIdentityIncludesOrderAndDoesNotFollowRuntimeType()
        {
            var derived = new DerivedPayload();
            BasePayload received = null;
            bus.Subscribe<BasePayload>(Key, value => received = value);
            bus.Publish<BasePayload>(Key, derived);
            Assert.That(received, Is.SameAs(derived));
            Assert.Throws<InvalidOperationException>(() => bus.Publish(Key, derived));
            Assert.Throws<InvalidOperationException>(() => bus.Subscribe<DerivedPayload>(Key, ReceiveDerived));
            bus.Subscribe<int, string>(Key + ".pair", ReceivePair);
            Assert.Throws<InvalidOperationException>(() => bus.Publish(Key + ".pair", "text", 1));
            bus.Subscribe<int, string, BasePayload>(Key + ".triple", ReceiveTriple);
            Assert.Throws<InvalidOperationException>(() => bus.Publish(Key + ".triple", 1, "text", derived));
            bus.Publish<int, string, BasePayload>(Key + ".triple", 1, "text", derived);
        }

        [Test]
        public void SharedDtoCrossesAssemblyBoundaryAndSameNamedDifferentTypeIsRejected()
        {
            Assert.That(typeof(SharedEventPayload).Assembly, Is.Not.EqualTo(GetType().Assembly));
            Assert.That(typeof(SharedEventPayload).Name, Is.EqualTo(typeof(Local.SharedEventPayload).Name));
            var consumer = new SharedPayloadConsumer();
            var payload = new SharedEventPayload { Value = 23 };
            consumer.Subscribe(bus, Key);
            bus.Publish(Key, payload);
            Assert.That(consumer.Received, Is.SameAs(payload));
            Assert.That(consumer.Calls, Is.EqualTo(1));
            Assert.Throws<InvalidOperationException>(() => bus.Publish(Key, new Local.SharedEventPayload()));
            consumer.Unsubscribe(bus, Key);
            bus.Publish(Key, payload);
            Assert.That(consumer.Calls, Is.EqualTo(1));
        }

        [Test]
        public void ContravariantCallbacksKeepCallSiteSignatureAndActualDelegateIdentity()
        {
            var listener = new ObjectListener();
            Action<object> objectAction = listener.Receive;
            Action<string> contravariant = objectAction;
            Action<string> stringAction = listener.Receive;
            Assert.That(contravariant.GetType(), Is.EqualTo(typeof(Action<object>)));
            Assert.That(stringAction.GetType(), Is.EqualTo(typeof(Action<string>)));
            // Some Mono versions consider these delegates equal despite their distinct actual types.
            // The EventBus contract must keep them independent on every supported runtime.
            bus.Subscribe<string>(Key, contravariant);
            bus.Subscribe<string>(Key, stringAction);
            Assert.Throws<InvalidOperationException>(() => bus.Subscribe<string>(Key, new Action<object>(listener.Receive)));
            Assert.Throws<InvalidOperationException>(() => bus.Publish<object>(Key, "payload"));
            bus.Publish(Key, "payload");
            Assert.That(listener.Calls, Is.EqualTo(2));
            bus.Unsubscribe<string>(Key, new Action<object>(listener.Receive));
            bus.Publish(Key, "payload");
            Assert.That(listener.Calls, Is.EqualTo(3));
            bus.Unsubscribe<string>(Key, stringAction);
            bus.Publish(Key, "payload");
            Assert.That(listener.Calls, Is.EqualTo(3));
        }

        [TestCase(0), TestCase(1), TestCase(2), TestCase(3)]
        public void SelfUnsubscribeAndUnknownUnsubscribeAreImmediateAndIdempotent(int arity)
        {
            var c = new EventCase(bus, arity);
            var listener = new Listener();
            Delegate callback = c.Callback(listener);
            listener.Effect = () => c.Unsubscribe(Key, callback);
            c.Subscribe(Key, callback);
            c.Unsubscribe(Key, c.Callback(new Listener()));
            c.Publish(Key);
            c.Publish(Key);
            Assert.That(listener.Calls, Is.EqualTo(1));
        }

        [TestCase(0), TestCase(1), TestCase(2), TestCase(3)]
        public void ResubscriptionAndNewListenersJoinNestedPublishButNotCurrentRound(int arity)
        {
            var c = new EventCase(bus, arity);
            var calls = new List<string>();
            bool outer = true;
            var second = new Listener { Effect = () => calls.Add("second") };
            var added = new Listener { Effect = () => calls.Add("added") };
            var first = new Listener
            {
                Effect = () =>
                {
                    calls.Add(outer ? "outer" : "nested");
                    if (!outer) return;
                    outer = false;
                    c.Unsubscribe(Key, c.Callback(second));
                    c.Subscribe(Key, c.Callback(second));
                    c.Subscribe(Key, c.Callback(added));
                    c.Publish(Key);
                }
            };
            c.Subscribe(Key, c.Callback(first));
            c.Subscribe(Key, c.Callback(second));
            c.Publish(Key);
            Assert.That(calls, Is.EqualTo(new[] { "outer", "nested", "second", "added" }));
            calls.Clear();
            c.Publish(Key);
            Assert.That(calls, Is.EqualTo(new[] { "nested", "second", "added" }));
        }

        [TestCase(0), TestCase(1), TestCase(2), TestCase(3)]
        public void GrowingStorageThenRemovingPendingListenerDoesNotReadStaleSlots(int arity)
        {
            var c = new EventCase(bus, arity);
            var pending = new Listener();
            var last = new Listener();
            var added = new Listener[2048];
            for (int i = 0; i < added.Length; i++) added[i] = new Listener();
            bool grown = false;
            var first = new Listener
            {
                Effect = () =>
                {
                    if (grown) return;
                    grown = true;
                    foreach (Listener listener in added) c.Subscribe(Key, c.Callback(listener));
                    c.Unsubscribe(Key, c.Callback(pending));
                }
            };
            c.Subscribe(Key, c.Callback(first));
            c.Subscribe(Key, c.Callback(pending));
            c.Subscribe(Key, c.Callback(last));
            c.Publish(Key);
            Assert.That(pending.Calls, Is.Zero);
            Assert.That(last.Calls, Is.EqualTo(1));
            Assert.That(added.All(listener => listener.Calls == 0), Is.True);
            c.Publish(Key);
            Assert.That(pending.Calls, Is.Zero);
            Assert.That(last.Calls, Is.EqualTo(2));
            Assert.That(added.All(listener => listener.Calls == 1), Is.True);
        }

        [TestCase(0), TestCase(1), TestCase(2), TestCase(3)]
        public void ManyRemovalsPreserveSurvivorOrderAndAllowReregistration(int arity)
        {
            var c = new EventCase(bus, arity);
            var order = new List<int>();
            var callbacks = new Delegate[1024];
            for (int i = 0; i < callbacks.Length; i++)
            {
                int id = i;
                callbacks[i] = c.Callback(new Listener { Effect = () => order.Add(id) });
                c.Subscribe(Key, callbacks[i]);
            }
            for (int i = 0; i < 768; i++) c.Unsubscribe(Key, callbacks[i]);
            c.Publish(Key);
            Assert.That(order, Is.EqualTo(Enumerable.Range(768, 256).ToArray()));
            c.Subscribe(Key, callbacks[0]);
            order.Clear();
            c.Publish(Key);
            Assert.That(order, Is.EqualTo(Enumerable.Range(768, 256).Concat(new[] { 0 }).ToArray()));
        }

        [TestCase(0), TestCase(1), TestCase(2), TestCase(3)]
        public void ReleaseDuringPublishStopsOldRoundAndCannotCleanNewInstance(int arity)
        {
            var c = new EventCase(bus, arity);
            var pending = new Listener();
            var freshListener = new Listener();
            EventBus fresh = null;
            bool emptyAfterRelease = false;
            var first = new Listener
            {
                Effect = () =>
                {
                    EventBus.ReleaseInstance();
                    emptyAfterRelease = RegisteredSingleton() == null;
                    fresh = EventBus.Instance;
                    var next = new EventCase(fresh, arity);
                    next.Subscribe(Key, next.Callback(freshListener));
                    next.Publish(Key);
                }
            };
            c.Subscribe(Key, c.Callback(first));
            c.Subscribe(Key, c.Callback(pending));
            c.Publish(Key);
            Assert.That(first.Calls, Is.EqualTo(1));
            Assert.That(emptyAfterRelease, Is.True, "Release itself must not construct a replacement.");
            Assert.That(pending.Calls, Is.Zero);
            Assert.That(fresh, Is.Not.SameAs(bus));
            Assert.That(EventBus.Instance, Is.SameAs(fresh));
            Assert.Throws<InvalidOperationException>(() => c.Subscribe(Key, c.Callback(pending)));
            Assert.Throws<InvalidOperationException>(() => c.Publish(Key));
            c.Unsubscribe(Key, c.Callback(pending));
            c.Unsubscribe(Key, c.Callback(pending));
            Assert.Throws<ArgumentNullException>(() => c.Unsubscribe(Key, null));
            Assert.Throws<ArgumentException>(() => c.Unsubscribe(Key, Delegate.Combine(c.Callback(pending), c.Callback(pending))));
            new EventCase(fresh, arity).Publish(Key);
            Assert.That(freshListener.Calls, Is.EqualTo(2));
        }

        [Test]
        public void ReleaseClearsHeldCallbacksSynchronouslyEvenInsideActivePublish()
        {
            IList callbacks = null;
            IDictionary indices = null;
            int countDuringRelease = -1;
            int indicesDuringRelease = -1;
            bool allSlotsCleared = false;
            bus.Subscribe(Key, () =>
            {
                EventBus.ReleaseInstance();
                countDuringRelease = callbacks.Count;
                indicesDuringRelease = indices.Count;
                allSlotsCleared = callbacks.Cast<object>().All(callback => callback == null);
            });
            bus.Subscribe(Key, new Listener().Receive);
            // The Task explicitly requires synchronously cleared indices/slots, stable Count during
            // dispatch, and delayed Clear. Observe those containers directly: conservative GC stacks
            // can retain an otherwise unreachable delegate and make a WeakReference nondeterministic.
            var channels = (IDictionary)typeof(EventBus).GetField("_channels",
                BindingFlags.Instance | BindingFlags.NonPublic).GetValue(bus);
            object channel = channels[Key];
            callbacks = (IList)channel.GetType().GetField("_callbacks",
                BindingFlags.Instance | BindingFlags.NonPublic).GetValue(channel);
            indices = (IDictionary)channel.GetType().GetField("_indices",
                BindingFlags.Instance | BindingFlags.NonPublic).GetValue(channel);
            Assert.That(callbacks.Count, Is.EqualTo(2));
            Assert.That(indices.Count, Is.EqualTo(2));
            bus.Publish(Key);
            Assert.That(countDuringRelease, Is.EqualTo(2), "An active Publish must keep its original slot indices.");
            Assert.That(indicesDuringRelease, Is.Zero, "Release must drop callback references in the lookup immediately.");
            Assert.That(allSlotsCleared, Is.True, "Release must drop every list-held callback before Publish returns.");
            Assert.That(callbacks.Count, Is.Zero, "The old container must finish its deferred cleanup on Publish exit.");
        }

        [Test]
        public void ReleaseWithoutAccessStaysEmptyAndNextInstanceHasNoSignatureOrListeners()
        {
            bus.Subscribe<int>(Key, ReceiveInt);
            EventBus.ReleaseInstance();
            EventBus.ReleaseInstance();
            Assert.That(RegisteredSingleton(), Is.Null);
            EventBus fresh = EventBus.Instance;
            Assert.That(fresh, Is.Not.SameAs(bus));
            Assert.DoesNotThrow(() => fresh.Subscribe(Key, ReceiveNone));
            Assert.Throws<InvalidOperationException>(() => bus.Publish<int>(Key, 1));
            bus.Unsubscribe<int>(Key, ReceiveInt);
            Assert.That(EventBus.Instance, Is.SameAs(fresh));
        }

        [TestCase(0), TestCase(1), TestCase(2), TestCase(3)]
        public void ConsecutiveCallbackFailuresUseRealLogAndDoNotAggregateOrStopDispatch(int arity)
        {
            var c = new EventCase(bus, arity);
            var firstError = new ApplicationException("first listener failed");
            var secondError = new ApplicationException("second listener failed");
            var valid = new Listener();
            c.Subscribe(Key, c.Callback(new Listener { Effect = () => throw firstError }));
            c.Subscribe(Key, c.Callback(new Listener { Effect = () => throw secondError }));
            c.Subscribe(Key, c.Callback(valid));
            using (var capture = new EventLogCapture())
            {
                Assert.DoesNotThrow(() => c.Publish(Key));
                Assert.That(capture.Exceptions, Is.EqualTo(new[] { firstError, secondError }));
                Assert.That(valid.Calls, Is.EqualTo(1));
            }
        }

        [TestCase(LogLevel.Info), TestCase(LogLevel.Warning), TestCase(LogLevel.Error)]
        public void ErrorReportingHonorsRealLogSwitchAndThreshold(LogLevel minimum)
        {
            bus.Subscribe(Key, ThrowListener);
            using (var capture = new EventLogCapture())
            {
                UtilsLog.SetLevel(minimum);
                UtilsLog.SetEnabled(false);
                bus.Publish(Key);
                Assert.That(capture.Calls, Is.Zero);
                UtilsLog.SetEnabled(true);
                bus.Publish(Key);
                Assert.That(capture.Calls, Is.EqualTo(1));
                Assert.That(capture.Exceptions[0].StackTrace, Does.Contain(nameof(ThrowListener)));
            }
        }

        [Test]
        public void ThrowingReportHandlerKeepsLaterListenersAndAttemptsEachReport()
        {
            int received = 0;
            bus.Subscribe(Key, ThrowListener);
            bus.Subscribe(Key, ThrowOtherListener);
            bus.Subscribe(Key, () => received++);
            using (var capture = new EventLogCapture())
            {
                capture.Report = () => throw new ApplicationException("logger failed");
                Assert.DoesNotThrow(() => bus.Publish(Key));
                Assert.That(capture.Calls, Is.EqualTo(2), "Each listener failure gets its own report attempt.");
                Assert.That(capture.Exceptions.Select(exception => exception.Message),
                    Is.EqualTo(new[] { "listener failed", "other listener failed" }));
                Assert.That(received, Is.EqualTo(1));
            }
        }

        [Test]
        public void ReportingReentrancySkipsNestedReportAndKeepsBothDispatches()
        {
            int nestedReceived = 0;
            int outerReceived = 0;
            bus.Subscribe(Key, ThrowListener);
            bus.Subscribe(Key, () => outerReceived++);
            bus.Subscribe(Key + ".nested", ThrowOtherListener);
            bus.Subscribe(Key + ".nested", () => nestedReceived++);
            using (var capture = new EventLogCapture())
            {
                capture.Report = () => bus.Publish(Key + ".nested");
                Assert.DoesNotThrow(() => bus.Publish(Key));
                Assert.That(capture.Calls, Is.EqualTo(1));
                Assert.That(capture.Exceptions[0].Message, Is.EqualTo("listener failed"));
                Assert.That(nestedReceived, Is.EqualTo(1));
                Assert.That(outerReceived, Is.EqualTo(1));
            }
        }

        [Test]
        public void ReportGuardAlsoCoversNewInstanceCreatedDuringReport()
        {
            int freshReceived = 0;
            EventBus fresh = null;
            bus.Subscribe(Key, ThrowListener);
            using (var capture = new EventLogCapture())
            {
                capture.Report = () =>
                {
                    EventBus.ReleaseInstance();
                    fresh = EventBus.Instance;
                    fresh.Subscribe(Key, ThrowOtherListener);
                    fresh.Subscribe(Key, () => freshReceived++);
                    fresh.Publish(Key);
                };
                Assert.DoesNotThrow(() => bus.Publish(Key));
                Assert.That(capture.Calls, Is.EqualTo(1));
                Assert.That(capture.Exceptions[0].Message, Is.EqualTo("listener failed"));
                Assert.That(freshReceived, Is.EqualTo(1));
                Assert.That(EventBus.Instance, Is.SameAs(fresh));
            }
        }

        [Test]
        public void ReportGuardResetsAfterFailureAndReportsTheOriginalExceptionAgain()
        {
            var valid = new Listener();
            var original = new ApplicationException("listener failed");
            bus.Subscribe(Key, () => throw original);
            bus.Subscribe(Key, valid.Receive);
            using (var capture = new EventLogCapture())
            {
                capture.Report = () => throw new ApplicationException("logger failed");
                Assert.DoesNotThrow(() => bus.Publish(Key));
                Assert.That(capture.Calls, Is.EqualTo(1));
                Assert.That(capture.Exceptions[0], Is.SameAs(original));
                Assert.That(valid.Calls, Is.EqualTo(1));

                capture.Report = null;
                Assert.DoesNotThrow(() => bus.Publish(Key));
                Assert.That(capture.Calls, Is.EqualTo(2));
                Assert.That(capture.Exceptions[1], Is.SameAs(original));
                Assert.That(valid.Calls, Is.EqualTo(2));
            }
        }

        private static EventBus RegisteredSingleton()
        {
            return (EventBus)typeof(Singleton<EventBus>)
                .GetField("_instance", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
        }

        private static void ReceiveNone() { }
        private static void ReceiveInt(int value) { }
        private static void ReceivePair(int number, string text) { }
        private static void ReceiveTriple(int number, string text, BasePayload payload) { }
        private static void ReceiveDerived(DerivedPayload payload) { }
        private static void ThrowListener()
        {
            throw new ApplicationException("listener failed");
        }
        private static void ThrowOtherListener()
        {
            throw new ApplicationException("other listener failed");
        }
        private class BasePayload { }
        private sealed class DerivedPayload : BasePayload { }
        private sealed class ObjectListener
        {
            internal int Calls;
            internal void Receive(object value) { Calls++; }
        }
    }
}

namespace GameSDK
{
    internal static class Local
    {
        internal sealed class SharedEventPayload { }
    }
}
