using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using UnityEngine;

namespace GameSDK.Tests
{
    [TestFixture]
    public sealed class UtilsLogTests
    {
        private LogCapture capture;

        [SetUp]
        public void SetUp()
        {
            capture = new LogCapture();
            RestoreBaseline();
        }

        [TearDown]
        public void TearDown()
        {
            try
            {
                RestoreBaseline();
            }
            finally
            {
                capture.Dispose();
            }
        }

        [TestCase(true, LogLevel.Info, 4)]
        [TestCase(true, LogLevel.Warning, 3)]
        [TestCase(true, LogLevel.Error, 2)]
        [TestCase(false, LogLevel.Info, 0)]
        [TestCase(false, LogLevel.Warning, 0)]
        [TestCase(false, LogLevel.Error, 0)]
        public void EverySwitchAndThresholdCombinationFiltersBeforeForwarding(bool enabled, LogLevel level, int count)
        {
            UtilsLog.SetEnabled(enabled);
            UtilsLog.SetLevel(level);

            EmitEveryLevel();

            var allTypes = new[] { LogType.Log, LogType.Warning, LogType.Error, LogType.Exception };
            var expected = new LogType[count];
            Array.Copy(allTypes, allTypes.Length - count, expected, 0, count);
            Assert.That(capture.Entries.ConvertAll(entry => entry.Type), Is.EqualTo(expected));
            Assert.That(capture.CallCount, Is.EqualTo(count));
        }

        [Test]
        public void RepeatedChangesApplyImmediatelyAndNeverReplayFilteredMessages()
        {
            UtilsLog.Log("first");
            UtilsLog.SetLevel(LogLevel.Warning);
            UtilsLog.SetLevel(LogLevel.Warning);
            UtilsLog.Log("filtered info");
            UtilsLog.Warning("second");
            UtilsLog.SetLevel(LogLevel.Error);
            UtilsLog.Warning("filtered warning");
            UtilsLog.Error("third");
            UtilsLog.SetEnabled(false);
            UtilsLog.SetEnabled(false);
            EmitEveryLevel();
            Assert.That(capture.CallCount, Is.EqualTo(3));

            UtilsLog.SetEnabled(true);
            UtilsLog.SetEnabled(true);
            Assert.That(capture.CallCount, Is.EqualTo(3), "重新开启不能补发被过滤消息。");
            UtilsLog.Log("still below threshold");
            UtilsLog.Warning("still below threshold");
            UtilsLog.Error("fourth");
            UtilsLog.SetLevel(LogLevel.Info);
            UtilsLog.Log("fifth");

            Assert.That(capture.Entries.ConvertAll(entry => entry.Arguments[0]),
                Is.EqualTo(new[] { "first", "second", "third", "fourth", "fifth" }));
            Assert.That(capture.CallCount, Is.EqualTo(5));
        }

        [Test]
        public void ThresholdCanChangeWhileDisabledAndIsRetainedWhenReenabled()
        {
            UtilsLog.SetEnabled(false);
            UtilsLog.SetLevel(LogLevel.Warning);
            EmitEveryLevel();
            Assert.That(capture.CallCount, Is.Zero);

            UtilsLog.SetEnabled(true);
            Assert.That(capture.CallCount, Is.Zero);
            EmitEveryLevel();
            Assert.That(capture.Entries.ConvertAll(entry => entry.Type),
                Is.EqualTo(new[] { LogType.Warning, LogType.Error, LogType.Exception }));
        }

        [TestCase(-1, true)]
        [TestCase(3, true)]
        [TestCase(int.MinValue, true)]
        [TestCase(int.MaxValue, true)]
        [TestCase(-1, false)]
        [TestCase(3, false)]
        [TestCase(int.MinValue, false)]
        [TestCase(int.MaxValue, false)]
        public void InvalidLevelThrowsWithoutChangingSwitchOrThreshold(int value, bool enabled)
        {
            UtilsLog.SetLevel(LogLevel.Warning);
            UtilsLog.SetEnabled(enabled);

            var failure = Assert.Throws<ArgumentOutOfRangeException>(
                () => UtilsLog.SetLevel((LogLevel)value));
            Assert.That(failure.ParamName, Is.EqualTo("level"));
            Assert.That(capture.CallCount, Is.Zero);
            EmitEveryLevel();
            Assert.That(capture.CallCount, Is.EqualTo(enabled ? 3 : 0));

            capture.Entries.Clear();
            UtilsLog.SetEnabled(true);
            EmitEveryLevel();
            Assert.That(capture.Entries.ConvertAll(entry => entry.Type),
                Is.EqualTo(new[] { LogType.Warning, LogType.Error, LogType.Exception }));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void NullExceptionAlwaysThrowsWithoutChangingConfiguration(bool enabled)
        {
            UtilsLog.SetLevel(LogLevel.Error);
            UtilsLog.SetEnabled(enabled);

            var failure = Assert.Throws<ArgumentNullException>(() => UtilsLog.Exception(null));
            Assert.That(failure.ParamName, Is.EqualTo("exception"));
            Assert.That(capture.CallCount, Is.Zero);
            EmitEveryLevel();
            Assert.That(capture.CallCount, Is.EqualTo(enabled ? 2 : 0));

            capture.Entries.Clear();
            UtilsLog.SetEnabled(true);
            EmitEveryLevel();
            Assert.That(capture.Entries.ConvertAll(entry => entry.Type),
                Is.EqualTo(new[] { LogType.Error, LogType.Exception }));
        }

        [TestCase(LogType.Log)]
        [TestCase(LogType.Warning)]
        [TestCase(LogType.Error)]
        public void OrdinaryMessagesPreserveUnityTypeAndOriginalString(LogType type)
        {
            const string message = "原消息 {0} <tag>\n第二行  ";
            EmitOrdinary(type, message, true);

            Assert.That(capture.Entries, Has.Count.EqualTo(1));
            Assert.That(capture.Entries[0].Type, Is.EqualTo(type));
            Assert.That(capture.Entries[0].Context, Is.Null);
            Assert.That(capture.Entries[0].Format, Is.EqualTo("{0}"));
            Assert.That(capture.Entries[0].Arguments, Has.Length.EqualTo(1));
            Assert.That(capture.Entries[0].Arguments[0], Is.SameAs(message));
        }

        [TestCase(LogType.Log, null)]
        [TestCase(LogType.Log, "")]
        [TestCase(LogType.Warning, null)]
        [TestCase(LogType.Warning, "")]
        [TestCase(LogType.Error, null)]
        [TestCase(LogType.Error, "")]
        public void NullAndEmptyMessagesMatchDirectUnitySemantics(LogType type, string message)
        {
            EmitOrdinary(type, message, false);
            EmitOrdinary(type, message, true);

            Assert.That(capture.Entries, Has.Count.EqualTo(2));
            LogCapture.Entry direct = capture.Entries[0];
            LogCapture.Entry wrapped = capture.Entries[1];
            Assert.That(wrapped.Type, Is.EqualTo(direct.Type));
            Assert.That(wrapped.Context, Is.SameAs(direct.Context));
            Assert.That(wrapped.Format, Is.EqualTo(direct.Format));
            Assert.That(wrapped.Arguments, Is.EqualTo(direct.Arguments));
        }

        [Test]
        public void ExceptionPreservesOriginalInstanceStackAndOptionalContext()
        {
            var context = new GameObject("UtilsLog exception test context");
            try
            {
                Exception original = CaptureOriginalException();
                string originalStack = original.StackTrace;
                UtilsLog.Exception(original);
                UtilsLog.Exception(original, context);

                Assert.That(capture.Entries, Has.Count.EqualTo(2));
                Assert.That(capture.Entries[0].Type, Is.EqualTo(LogType.Exception));
                Assert.That(capture.Entries[0].Exception, Is.SameAs(original));
                Assert.That(capture.Entries[0].Context, Is.Null);
                Assert.That(capture.Entries[1].Type, Is.EqualTo(LogType.Exception));
                Assert.That(capture.Entries[1].Exception, Is.SameAs(original));
                Assert.That(capture.Entries[1].Context, Is.SameAs(context));
                Assert.That(originalStack, Does.Contain(nameof(ThrowOriginalException)));
                Assert.That(original.StackTrace, Is.EqualTo(originalStack));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(context);
            }
        }

        [TestCase(LogType.Log)]
        [TestCase(LogType.Warning)]
        [TestCase(LogType.Error)]
        [TestCase(LogType.Exception)]
        public void HandlerFailurePropagatesUnchangedWithoutRecursiveReporting(LogType type)
        {
            var originalFailure = new InvalidOperationException("handler failed");
            capture.Failure = originalFailure;

            var actual = Assert.Throws<InvalidOperationException>(() =>
            {
                if (type == LogType.Exception)
                    UtilsLog.Exception(new ArgumentException("reported exception"));
                else
                    EmitOrdinary(type, "reported message", true);
            });

            Assert.That(actual, Is.SameAs(originalFailure));
            Assert.That(capture.CallCount, Is.EqualTo(1));
        }

        [Test]
        public void FilteredCallsNeverReachFailingHandler()
        {
            capture.Failure = new InvalidOperationException("must not be reached");
            UtilsLog.SetEnabled(false);
            Assert.DoesNotThrow(EmitEveryLevel);
            UtilsLog.SetEnabled(true);
            UtilsLog.SetLevel(LogLevel.Error);
            Assert.DoesNotThrow(() => UtilsLog.Log("filtered info"));
            Assert.DoesNotThrow(() => UtilsLog.Warning("filtered warning"));
            Assert.That(capture.CallCount, Is.Zero);
        }

        [Test]
        public void WrapperConfigurationAndCallsDoNotModifyUnityGlobalState()
        {
            ILogger logger = Debug.unityLogger;
            logger.logEnabled = false;
            logger.filterLogType = LogType.Warning;

            UtilsLog.SetEnabled(false);
            UtilsLog.SetLevel(LogLevel.Error);
            EmitEveryLevel();
            UtilsLog.SetEnabled(true);
            UtilsLog.SetLevel(LogLevel.Info);
            EmitEveryLevel();

            Assert.That(logger.logEnabled, Is.False);
            Assert.That(logger.filterLogType, Is.EqualTo(LogType.Warning));
            Assert.That(logger.logHandler, Is.SameAs(capture));
            Assert.That(capture.CallCount, Is.Zero);
        }

        [Test]
        public void WrapperSwitchAndThresholdDoNotSuppressDirectUnityLogs()
        {
            UtilsLog.SetLevel(LogLevel.Error);
            UtilsLog.SetEnabled(false);
            EmitEveryLevel();
            Debug.Log("direct info");
            Debug.LogWarning("direct warning");
            Debug.LogError("direct error");
            var exception = new InvalidOperationException("direct exception");
            Debug.LogException(exception);

            Assert.That(capture.Entries.ConvertAll(entry => entry.Type),
                Is.EqualTo(new[] { LogType.Log, LogType.Warning, LogType.Error, LogType.Exception }));
            Assert.That(capture.Entries[3].Exception, Is.SameAs(exception));
        }

        [Test]
        public void UnityGlobalSwitchAndFilterStillGovernForwardedMessages()
        {
            ILogger logger = Debug.unityLogger;
            logger.logEnabled = false;
            EmitEveryLevel();
            Assert.That(capture.CallCount, Is.Zero);

            logger.logEnabled = true;
            logger.filterLogType = LogType.Warning;
            UtilsLog.Log("Unity filtered wrapper info");
            Debug.Log("Unity filtered direct info");
            UtilsLog.Warning("wrapper warning");
            Debug.LogWarning("direct warning");
            UtilsLog.Error("wrapper error");
            Debug.LogError("direct error");

            Assert.That(capture.Entries.ConvertAll(entry => entry.Type),
                Is.EqualTo(new[] { LogType.Warning, LogType.Warning, LogType.Error, LogType.Error }));
            Assert.That(logger.filterLogType, Is.EqualTo(LogType.Warning));
            Assert.That(logger.logHandler, Is.SameAs(capture));

            logger.filterLogType = LogType.Log;
            UtilsLog.Log("Unity accepts wrapper info again");
            Assert.That(capture.Entries[capture.Entries.Count - 1].Type, Is.EqualTo(LogType.Log));
        }

        private static void RestoreBaseline()
        {
            UtilsLog.SetEnabled(true);
            UtilsLog.SetLevel(LogLevel.Info);
        }

        private static void EmitEveryLevel()
        {
            UtilsLog.Log("info");
            UtilsLog.Warning("warning");
            UtilsLog.Error("error");
            UtilsLog.Exception(new InvalidOperationException("exception"));
        }

        private static void EmitOrdinary(LogType type, string message, bool wrapped)
        {
            switch (type)
            {
                case LogType.Log:
                    if (wrapped) UtilsLog.Log(message); else Debug.Log(message);
                    break;
                case LogType.Warning:
                    if (wrapped) UtilsLog.Warning(message); else Debug.LogWarning(message);
                    break;
                case LogType.Error:
                    if (wrapped) UtilsLog.Error(message); else Debug.LogError(message);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(type));
            }
        }

        private static Exception CaptureOriginalException()
        {
            try
            {
                ThrowOriginalException();
            }
            catch (InvalidOperationException exception)
            {
                return exception;
            }

            throw new AssertionException("原始异常未抛出。");
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ThrowOriginalException()
        {
            throw new InvalidOperationException("original throw location");
        }
    }
}
