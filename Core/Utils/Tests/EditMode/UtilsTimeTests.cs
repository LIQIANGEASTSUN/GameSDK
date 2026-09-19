using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace GameSDK
{
    [TestFixture]
    [Parallelizable(ParallelScope.None)]
    public sealed class UtilsTimeTests
    {
        private const long ServerSecond = 1700000000L;
        private const int RealtimeDecimalPlaces = 9;
        private static readonly FieldInfo Synced = GetField("_hasServerTimeSync");
        private static readonly FieldInfo Offset = GetField("_serverTimeOffsetSeconds");

        private readonly Dictionary<FieldInfo, object> _originalState = new Dictionary<FieldInfo, object>();

        [SetUp]
        public void SetUp()
        {
            _originalState.Clear();
            foreach (FieldInfo field in new[] { Synced, Offset })
                _originalState.Add(field, field.GetValue(null));

            Synced.SetValue(null, false);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (KeyValuePair<FieldInfo, object> state in _originalState)
                state.Key.SetValue(null, state.Value);
        }

        [Test]
        [Category("UnityClock")]
        public void UnsyncedGettersThrow()
        {
            Assert.Throws<InvalidOperationException>(() => UtilsTime.TimeServerSecond());
            Assert.Throws<InvalidOperationException>(() => UtilsTime.TimeServerMillsecond());
        }

        [Test]
        [Category("UnityClock")]
        public void SyncAcceptsSecondsAndUsesUnityRealtime()
        {
            decimal syncBefore = decimal.Round((decimal)Time.realtimeSinceStartupAsDouble, RealtimeDecimalPlaces);
            UtilsTime.SyncServerTime(ServerSecond);
            decimal syncAfter = decimal.Round((decimal)Time.realtimeSinceStartupAsDouble, RealtimeDecimalPlaces);

            AssertServerTimeWithinSampleBounds(ServerSecond, syncBefore, syncAfter);
        }

        [TestCase(0d)]
        [TestCase(0.5d)]
        [TestCase(0.999d)]
        [TestCase(1d)]
        [TestCase(1.5d)]
        [TestCase(63d)]
        [TestCase(65d)]
        [TestCase(128d)]
        [Category("UnityClock")]
        public void SecondsAdvanceOnlyAfterEachCompleteElapsedSecond(double elapsed)
        {
            UtilsTime.SyncServerTime(ServerSecond);
            decimal syncTime = SetElapsedSeconds(ServerSecond, elapsed);

            AssertSecondsWithinSampleBounds(ServerSecond, syncTime, syncTime);
        }

        [Test]
        [Category("UnityClock")]
        public void MillisecondsAndSecondsUseTheSameSynchronization()
        {
            UtilsTime.SyncServerTime(ServerSecond);
            decimal syncTime = SetElapsedSeconds(ServerSecond, 1.5d);

            AssertServerTimeWithinSampleBounds(ServerSecond, syncTime, syncTime);
        }

        [Test]
        [Category("UnityClock")]
        public void RepeatedSyncReplacesOffsetForBackwardAndForwardCorrections()
        {
            UtilsTime.SyncServerTime(ServerSecond);
            SetElapsedSeconds(ServerSecond, 30.5d);
            decimal syncBefore = decimal.Round((decimal)Time.realtimeSinceStartupAsDouble, RealtimeDecimalPlaces);
            UtilsTime.SyncServerTime(ServerSecond - 100L);
            decimal syncAfter = decimal.Round((decimal)Time.realtimeSinceStartupAsDouble, RealtimeDecimalPlaces);
            AssertServerTimeWithinSampleBounds(ServerSecond - 100L, syncBefore, syncAfter);

            decimal syncTime = SetElapsedSeconds(ServerSecond - 100L, 1d);
            AssertSecondsWithinSampleBounds(ServerSecond - 100L, syncTime, syncTime);
            syncBefore = decimal.Round((decimal)Time.realtimeSinceStartupAsDouble, RealtimeDecimalPlaces);
            UtilsTime.SyncServerTime(ServerSecond + 100L);
            syncAfter = decimal.Round((decimal)Time.realtimeSinceStartupAsDouble, RealtimeDecimalPlaces);
            AssertServerTimeWithinSampleBounds(ServerSecond + 100L, syncBefore, syncAfter);
        }

        [TestCase(9007199254740993L)]
        [TestCase(long.MinValue)]
        [TestCase(-7922816251426433759L)]
        [Category("UnityClock")]
        public void LongSecondValuesRemainExact(long second)
        {
            decimal syncBefore = decimal.Round((decimal)Time.realtimeSinceStartupAsDouble, RealtimeDecimalPlaces);
            UtilsTime.SyncServerTime(second);
            decimal syncAfter = decimal.Round((decimal)Time.realtimeSinceStartupAsDouble, RealtimeDecimalPlaces);
            AssertSecondsWithinSampleBounds(second, syncBefore, syncAfter);

            decimal syncTime = SetElapsedSeconds(second, 1d);
            AssertSecondsWithinSampleBounds(second, syncTime, syncTime);
        }

        [TestCase(0.5d)]
        [TestCase(1.5d)]
        [Category("UnityClock")]
        public void NegativeServerTimeRoundsDownOnBothSidesOfTheEpoch(double elapsed)
        {
            UtilsTime.SyncServerTime(-1L);
            decimal syncTime = SetElapsedSeconds(-1L, elapsed);

            AssertServerTimeWithinSampleBounds(-1L, syncTime, syncTime);
        }

        [Test]
        [Category("UnityClock")]
        public void SecondOverflowThrowsInsteadOfWrapping()
        {
            UtilsTime.SyncServerTime(long.MaxValue);
            SetElapsedSeconds(long.MaxValue, 1.5d);
            Assert.Throws<OverflowException>(() => UtilsTime.TimeServerSecond());
        }

        [TestCase(9223372036854776L, 0d)]
        [TestCase(9223372036854775L, 1.5d)]
        [Category("UnityClock")]
        public void MillisecondMultiplicationAndAdditionOverflowThrow(long second, double elapsed)
        {
            UtilsTime.SyncServerTime(second);
            SetElapsedSeconds(second, elapsed);
            Assert.Throws<OverflowException>(() => UtilsTime.TimeServerMillsecond());
        }

        [TestCase("2023-11-14T22:13:20Z", TimestampUnit.Seconds, 1700000000L)]
        [TestCase("2023-11-14T22:13:20Z", TimestampUnit.Milliseconds, 1700000000000L)]
        [TestCase("1970-01-01T00:00:00Z", TimestampUnit.Seconds, 0L)]
        [TestCase("1970-01-01T00:00:00Z", TimestampUnit.Milliseconds, 0L)]
        [TestCase("1969-12-31T23:59:59.500Z", TimestampUnit.Seconds, -1L)]
        [TestCase("1969-12-31T23:59:59.500Z", TimestampUnit.Milliseconds, -500L)]
        [TestCase("1969-12-31T23:59:59.9999Z", TimestampUnit.Milliseconds, -1L)]
        public void ConversionUsesUnixRoundingIncludingBeforeTheEpoch(string input, TimestampUnit unit, long expected)
        {
            bool success = UtilsTime.TryConvertToTimestamp(input, unit, out long timestamp);

            Assert.That(success, Is.True);
            Assert.That(timestamp, Is.EqualTo(expected));
        }

        [TestCase(null, TimestampUnit.Seconds)]
        [TestCase("", TimestampUnit.Seconds)]
        [TestCase(" \t\r\n", TimestampUnit.Seconds)]
        [TestCase("not a date", TimestampUnit.Seconds)]
        [TestCase(null, TimestampUnit.Milliseconds)]
        [TestCase("", TimestampUnit.Milliseconds)]
        [TestCase(" \t\r\n", TimestampUnit.Milliseconds)]
        [TestCase("not a date", TimestampUnit.Milliseconds)]
        public void InvalidDateReturnsFalseAndClearsTimestamp(string input, TimestampUnit unit)
        {
            long timestamp = 123L;
            bool success = UtilsTime.TryConvertToTimestamp(input, unit, out timestamp);

            Assert.That(success, Is.False);
            Assert.That(timestamp, Is.Zero);
        }

        [TestCase("2023-11-14T22:13:20Z", -1)]
        [TestCase("not a date", -1)]
        [TestCase("2023-11-14T22:13:20Z", 2)]
        [TestCase(null, 2)]
        public void InvalidUnitThrowsBeforeAttemptingToParseTheDate(string input, int unitValue)
        {
            var unit = (TimestampUnit)unitValue;
            var failure = Assert.Throws<ArgumentOutOfRangeException>(
                () => UtilsTime.TryConvertToTimestamp(input, unit, out _));

            Assert.That(failure.ParamName, Is.EqualTo("unit"));
            Assert.That(failure.ActualValue, Is.EqualTo(unit));
        }

        [Test]
        public void ConversionDoesNotWriteToConsoleOnSuccessOrFailure()
        {
            TextWriter originalOutput = Console.Out;
            TextWriter originalError = Console.Error;
            using (var output = new StringWriter())
            {
                try
                {
                    Console.SetOut(output);
                    Console.SetError(output);

                    Assert.That(UtilsTime.TryConvertToTimestamp(
                        "1969-12-31T23:59:59.500Z", TimestampUnit.Seconds, out long timestamp), Is.True);
                    Assert.That(timestamp, Is.EqualTo(-1L));
                    Assert.That(UtilsTime.TryConvertToTimestamp(
                        "not a date", TimestampUnit.Seconds, out timestamp), Is.False);
                    Assert.That(timestamp, Is.Zero);
                }
                finally
                {
                    Console.SetOut(originalOutput);
                    Console.SetError(originalError);
                }

                Assert.That(output.ToString(), Is.Empty);
            }
        }

        [TestCase("en-US", 1, 2)]
        [TestCase("en-GB", 2, 1)]
        public void ConversionPreservesCurrentCultureAndLocalTimezone(string culture, int month, int day)
        {
            CultureInfo originalCulture = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
                var expected = new DateTimeOffset(new DateTime(2025, month, day, 12, 0, 0, DateTimeKind.Local));
                bool success = UtilsTime.TryConvertToTimestamp(
                    "01/02/2025 12:00:00", TimestampUnit.Seconds, out long timestamp);

                Assert.That(success, Is.True);
                Assert.That(timestamp, Is.EqualTo(expected.ToUnixTimeSeconds()));
            }
            finally
            {
                CultureInfo.CurrentCulture = originalCulture;
            }
        }

        [Test]
        public void LocalGettersReturnCurrentUnixTime()
        {
            DateTimeOffset before = DateTimeOffset.UtcNow;
            long seconds = UtilsTime.TimeLocalSecond();
            long milliseconds = UtilsTime.TimeLocalMillsecond();
            DateTimeOffset after = DateTimeOffset.UtcNow;

            Assert.That(seconds, Is.InRange(before.ToUnixTimeSeconds(), after.ToUnixTimeSeconds()));
            Assert.That(milliseconds, Is.InRange(before.ToUnixTimeMilliseconds(), after.ToUnixTimeMilliseconds()));
        }

        private static decimal SetElapsedSeconds(long serverSecond, double elapsed)
        {
            // 只调整已有偏差，计时仍使用真实 Unity 时钟，不等待时间经过。
            decimal syncTime = decimal.Round((decimal)Time.realtimeSinceStartupAsDouble, RealtimeDecimalPlaces) - (decimal)elapsed;
            Offset.SetValue(null, serverSecond - syncTime);
            return syncTime;
        }

        private static void AssertSecondsWithinSampleBounds(long serverSecond, decimal syncBefore, decimal syncAfter)
        {
            decimal before = decimal.Round((decimal)Time.realtimeSinceStartupAsDouble, RealtimeDecimalPlaces);
            long seconds = UtilsTime.TimeServerSecond();
            decimal after = decimal.Round((decimal)Time.realtimeSinceStartupAsDouble, RealtimeDecimalPlaces);

            Assert.That(seconds, Is.InRange(
                checked(serverSecond + (long)decimal.Floor(Math.Max(0m, before - syncAfter))),
                checked(serverSecond + (long)decimal.Floor(after - syncBefore))));
        }

        private static void AssertServerTimeWithinSampleBounds(long serverSecond, decimal syncBefore, decimal syncAfter)
        {
            decimal before = decimal.Round((decimal)Time.realtimeSinceStartupAsDouble, RealtimeDecimalPlaces);
            long seconds = UtilsTime.TimeServerSecond();
            long milliseconds = UtilsTime.TimeServerMillsecond();
            decimal after = decimal.Round((decimal)Time.realtimeSinceStartupAsDouble, RealtimeDecimalPlaces);

            Assert.That(seconds, Is.InRange(
                checked(serverSecond + (long)decimal.Floor(Math.Max(0m, before - syncAfter))),
                checked(serverSecond + (long)decimal.Floor(after - syncBefore))));
            Assert.That(milliseconds, Is.InRange(
                checked(serverSecond * 1000L + (long)decimal.Floor(Math.Max(0m, before - syncAfter) * 1000m)),
                checked(serverSecond * 1000L + (long)decimal.Floor((after - syncBefore) * 1000m))));
        }

        private static FieldInfo GetField(string name)
        {
            return typeof(UtilsTime).GetField(name, BindingFlags.NonPublic | BindingFlags.Static)
                ?? throw new InvalidOperationException("Missing UtilsTime state: " + name);
        }
    }
}
