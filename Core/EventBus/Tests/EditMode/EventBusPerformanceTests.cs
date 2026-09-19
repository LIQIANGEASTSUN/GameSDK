using System;
using System.Collections;
using System.Diagnostics;
using System.Reflection;
using NUnit.Framework;

namespace GameSDK.Tests.EventBusTests
{
    // Explicit: select this fixture to measure all cases. Run without profiling/deep profiling.
    // Release/optimized builds and a warmed runtime are required for acceptance numbers.
    [TestFixture, Explicit("Performance acceptance: run all cases in one warmed, optimized environment.")]
    public sealed class EventBusPerformanceTests
    {
        private const string Key = "EventBus.Tests.Performance";
        private const int Iterations = 10000;
        private const int BatchSize = 1024;
        private const int BatchRounds = 100;

        [TearDown]
        public void TearDown()
        {
            EventBus.ReleaseInstance();
        }

        [TestCase(0, 1), TestCase(0, 32), TestCase(0, 256), TestCase(0, 1024)]
        [TestCase(1, 1), TestCase(1, 32), TestCase(1, 256), TestCase(1, 1024)]
        [TestCase(2, 1), TestCase(2, 32), TestCase(2, 256), TestCase(2, 1024)]
        [TestCase(3, 1), TestCase(3, 32), TestCase(3, 256), TestCase(3, 1024)]
        public void StablePublish(int arity, int listeners)
        {
            EventCase c = FreshCase(arity);
            Delegate[] callbacks = Callbacks(c, listeners);
            foreach (Delegate callback in callbacks) c.Subscribe(Key, callback);
            for (int i = 0; i < 2000; i++) c.Publish(Key);
            Action workload = () => { for (int i = 0; i < Iterations; i++) c.Publish(Key); };
            workload(); // Warm the measured delegate/loop as well as the underlying Publish calls.
            Sample[] samples = MeasureThree(workload);
            Report("publish", arity, listeners, Iterations, samples, 1000, 0);
        }

        [TestCase(0), TestCase(1), TestCase(2), TestCase(3)]
        public void BatchRegistrationAndRemovalIncludeSameMethodDifferentTargets(int arity)
        {
            EventCase warm = FreshCase(arity);
            Delegate[] callbacks = Callbacks(warm, BatchSize);
            foreach (Delegate callback in callbacks) warm.Subscribe(Key, callback);
            foreach (Delegate callback in callbacks) warm.Unsubscribe(Key, callback);
            var subscriptions = new Sample[3];
            var removals = new Sample[3];
            for (int sample = 0; sample < 3; sample++)
            {
                EventCase c = FreshCase(arity);
                long subscribeTicks = 0, subscribeBytes = 0, removeTicks = 0, removeBytes = 0;
                for (int round = 0; round < BatchRounds; round++)
                {
                    long bytes = GC.GetAllocatedBytesForCurrentThread();
                    long ticks = Stopwatch.GetTimestamp();
                    for (int i = 0; i < callbacks.Length; i++) c.Subscribe(Key, callbacks[i]);
                    subscribeTicks += Stopwatch.GetTimestamp() - ticks;
                    subscribeBytes += GC.GetAllocatedBytesForCurrentThread() - bytes;
                    bytes = GC.GetAllocatedBytesForCurrentThread();
                    ticks = Stopwatch.GetTimestamp();
                    for (int i = 0; i < callbacks.Length; i++) c.Unsubscribe(Key, callbacks[i]);
                    removeTicks += Stopwatch.GetTimestamp() - ticks;
                    removeBytes += GC.GetAllocatedBytesForCurrentThread() - bytes;
                }
                subscriptions[sample] = new Sample(subscribeTicks, subscribeBytes);
                removals[sample] = new Sample(removeTicks, removeBytes);
            }
            int operations = BatchSize * BatchRounds;
            Report("batch subscribe same method / distinct targets", arity, BatchSize, operations,
                subscriptions, 10000, 1024L * operations);
            Report("batch unsubscribe including compaction", arity, BatchSize, operations,
                removals, 10000, 128L * operations);
        }

        [TestCase(0), TestCase(1), TestCase(2), TestCase(3)]
        public void StableChurnIncludesPeriodicCompaction(int arity)
        {
            EventCase c = FreshCase(arity);
            Delegate[] callbacks = Callbacks(c, BatchSize);
            foreach (Delegate callback in callbacks) c.Subscribe(Key, callback);
            Action workload = () =>
            {
                for (int i = 0; i < Iterations; i++)
                {
                    c.Unsubscribe(Key, callbacks[0]);
                    c.Subscribe(Key, callbacks[0]);
                }
            };
            workload();
            Report("stable remove/add pair", arity, BatchSize, Iterations,
                MeasureThree(workload), 2500, 256L * Iterations);
        }

        [TestCase(0), TestCase(1), TestCase(2), TestCase(3)]
        public void NestedPublishing(int arity)
        {
            EventCase c = FreshCase(arity);
            bool inside = false;
            var listener = new Listener
            {
                Effect = () =>
                {
                    if (inside) return;
                    inside = true;
                    try { c.Publish(Key); }
                    finally { inside = false; }
                }
            };
            c.Subscribe(Key, c.Callback(listener));
            Action workload = () => { for (int i = 0; i < Iterations; i++) c.Publish(Key); };
            workload();
            Report("nested publish (outer + inner)", arity, 1, Iterations,
                MeasureThree(workload), 1000, 0);
        }

        [TestCase(0), TestCase(1), TestCase(2), TestCase(3)]
        public void GrowthAndCompactionBoundarySamples(int arity)
        {
            var growth = new Sample[3];
            var compaction = new Sample[3];
            EventCase warm = FreshCase(arity);
            Delegate[] callbacks = Callbacks(warm, 1025);
            foreach (Delegate callback in callbacks) warm.Subscribe(Key, callback);
            foreach (Delegate callback in callbacks) warm.Unsubscribe(Key, callback);
            for (int sample = 0; sample < 3; sample++)
            {
                EventCase c = FreshCase(arity);
                ReportStorage("before growth", arity, sample + 1);
                growth[sample] = Measure(() =>
                {
                    for (int i = 0; i < callbacks.Length; i++) c.Subscribe(Key, callbacks[i]);
                });
                ReportStorage("after growth to 1025", arity, sample + 1);
                // Start the removal sample from exactly 1024 live entries and no earlier tombstones.
                c = FreshCase(arity);
                for (int i = 0; i < 1024; i++) c.Subscribe(Key, callbacks[i]);
                ReportStorage("before removing 768 of 1024", arity, sample + 1);
                compaction[sample] = Measure(() =>
                {
                    for (int i = 0; i < 768; i++) c.Unsubscribe(Key, callbacks[i]);
                });
                ReportStorage("after removal / compaction", arity, sample + 1);
                c.Publish(Key);
            }
            Report("growth from empty through 1025 listeners", arity, 1025, 1025, growth, 1000, 1024L * 1025);
            Report("remove first 768 of 1024, including compaction", arity, 1024, 768, compaction, 1000, 128L * 768);
        }

        private static EventCase FreshCase(int arity)
        {
            EventBus.ReleaseInstance();
            return new EventCase(EventBus.Instance, arity);
        }

        private static Delegate[] Callbacks(EventCase c, int count)
        {
            var callbacks = new Delegate[count];
            for (int i = 0; i < count; i++) callbacks[i] = c.Callback(new Listener());
            return callbacks;
        }

        private static Sample[] MeasureThree(Action workload)
        {
            var result = new Sample[3];
            for (int i = 0; i < result.Length; i++) result[i] = Measure(workload);
            return result;
        }

        // Structural diagnostics are deliberately outside every timed/allocation interval.
        // EnsureCapacity(0) reads an existing Dictionary capacity without requesting growth.
        private static void ReportStorage(string phase, int arity, int sample)
        {
            var channels = (IDictionary)typeof(EventBus).GetField("_channels",
                BindingFlags.Instance | BindingFlags.NonPublic).GetValue(EventBus.Instance);
            object channel = channels[Key];
            int listCount = 0, listCapacity = 0, dictionaryCount = 0, dictionaryCapacity = 0;
            if (channel != null)
            {
                object callbacks = channel.GetType().GetField("_callbacks",
                    BindingFlags.Instance | BindingFlags.NonPublic).GetValue(channel);
                object indices = channel.GetType().GetField("_indices",
                    BindingFlags.Instance | BindingFlags.NonPublic).GetValue(channel);
                listCount = ((IList)callbacks).Count;
                listCapacity = (int)callbacks.GetType().GetProperty("Capacity").GetValue(callbacks);
                dictionaryCount = ((IDictionary)indices).Count;
                dictionaryCapacity = (int)indices.GetType().GetMethod("EnsureCapacity", new[] { typeof(int) })
                    .Invoke(indices, new object[] { 0 });
            }
            TestContext.WriteLine("Storage {0}, arity={1}, sample={2}: List Count={3}, Capacity={4}; Dictionary Count={5}, Capacity={6}",
                phase, arity, sample, listCount, listCapacity, dictionaryCount, dictionaryCapacity);
        }

        private static Sample Measure(Action workload)
        {
            long bytes = GC.GetAllocatedBytesForCurrentThread();
            long ticks = Stopwatch.GetTimestamp();
            workload();
            return new Sample(Stopwatch.GetTimestamp() - ticks, GC.GetAllocatedBytesForCurrentThread() - bytes);
        }

        private static void Report(string scenario, int arity, int listeners, int operations,
            Sample[] samples, double budgetMilliseconds, long budgetBytes)
        {
            var milliseconds = new double[3];
            var allocations = new long[3];
            for (int i = 0; i < samples.Length; i++)
            {
                milliseconds[i] = samples[i].Ticks * 1000.0 / Stopwatch.Frequency;
                allocations[i] = samples[i].Bytes;
                TestContext.WriteLine("{0}, arity={1}, listeners={2}, operations={3}, sample={4}: {5:F3} ms, {6} B ({7:F2} B/op)",
                    scenario, arity, listeners, operations, i + 1, milliseconds[i], allocations[i], allocations[i] / (double)operations);
            }
            Array.Sort(milliseconds);
            Array.Sort(allocations);
            TestContext.WriteLine("Median: {0:F3} ms, {1} B; budget <= {2} ms / <= {3} B. Runtime={4}, 64-bit={5}",
                milliseconds[1], allocations[1], budgetMilliseconds, budgetBytes, Environment.Version, Environment.Is64BitProcess);
            Assert.That(milliseconds[1], Is.LessThanOrEqualTo(budgetMilliseconds));
            if (budgetBytes == 0)
            {
                foreach (Sample sample in samples)
                    Assert.That(sample.Bytes, Is.Zero, "Every warmed stable-publish sample must allocate zero bytes.");
            }
            else Assert.That(allocations[1], Is.LessThanOrEqualTo(budgetBytes));
        }

        private struct Sample
        {
            internal readonly long Ticks;
            internal readonly long Bytes;
            internal Sample(long ticks, long bytes) { Ticks = ticks; Bytes = bytes; }
        }
    }
}
