using System;
using System.Collections;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace GameSDK
{
    // Run in an isolated saved scene. These named host operations own session cleanup;
    // neither the test nor production code installs automatic Play-state reset hooks.
    public sealed class EventBusSessionTests
    {
        private const string Prefix = "GameSDK.EventBus.SessionTests.";
        private const string Key = "EventBus.Tests.PlaySession";
        private static EventBus ownedBus;
        private static EventBus previousBus;

        [SetUp]
        public void SaveSettings()
        {
            Assert.That(Application.isPlaying, Is.False);
            EventBus.ReleaseInstance();
            ownedBus = null;
            previousBus = null;
            SessionState.SetBool(Prefix + "Enabled", EditorSettings.enterPlayModeOptionsEnabled);
            SessionState.SetInt(Prefix + "Options", (int)EditorSettings.enterPlayModeOptions);
            SessionState.SetBool(Prefix + "Saved", true);
            SessionState.SetInt(Prefix + "Calls", 0);
            SessionState.SetInt(Prefix + "EditingCalls", 0);
        }

        [UnityTearDown]
        public IEnumerator RestoreSettings()
        {
            HostCloseSession();
            if (Application.isPlaying) yield return new ExitPlayMode();
            EventBus.ReleaseInstance();
            ownedBus = null;
            previousBus = null;
            if (SessionState.GetBool(Prefix + "Saved", false))
            {
                EditorSettings.enterPlayModeOptions = (EnterPlayModeOptions)SessionState.GetInt(Prefix + "Options", 0);
                EditorSettings.enterPlayModeOptionsEnabled = SessionState.GetBool(Prefix + "Enabled", false);
            }
            SessionState.EraseBool(Prefix + "Saved");
            SessionState.EraseBool(Prefix + "Enabled");
            SessionState.EraseInt(Prefix + "Options");
            SessionState.EraseInt(Prefix + "Calls");
            SessionState.EraseInt(Prefix + "EditingCalls");
        }

        [UnityTest]
        public IEnumerator ExplicitHostClosesConsecutivePlaySessionsWithDomainReload()
        {
            Configure(false);
            HostOpenEditingSession();
            HostCloseSession();
            AssertInvalid(previousBus);
            yield return new EnterPlayMode();
            HostOpenPlaySession(1, false);
            HostCloseSession();
            AssertInvalid(previousBus);
            yield return new ExitPlayMode();
            VerifyExit(1, false);
            yield return new EnterPlayMode();
            HostOpenPlaySession(2, false);
            HostCloseSession();
            AssertInvalid(previousBus);
            yield return new ExitPlayMode();
            VerifyExit(2, false);
        }

        [UnityTest]
        public IEnumerator ExplicitHostControlsRetainedStateWithoutDomainReload()
        {
            Configure(true);
            HostOpenEditingSession();
            yield return new EnterPlayMode(false);
            VerifyUnreleasedEditingSessionIsRetained();
            HostCloseSession();
            AssertInvalid(previousBus);
            HostOpenPlaySession(1, true);
            HostCloseSession();
            AssertInvalid(previousBus);
            yield return new ExitPlayMode();
            VerifyExit(1, true);
            yield return new EnterPlayMode(false);
            HostOpenPlaySession(2, true);
            HostCloseSession();
            AssertInvalid(previousBus);
            yield return new ExitPlayMode();
            VerifyExit(2, true);
        }

        private static void Configure(bool disableDomainReload)
        {
            EditorSettings.enterPlayModeOptionsEnabled = disableDomainReload;
            EditorSettings.enterPlayModeOptions = disableDomainReload
                ? EnterPlayModeOptions.DisableDomainReload : EnterPlayModeOptions.None;
        }

        private static void HostOpenEditingSession()
        {
            ownedBus = EventBus.Instance;
            ownedBus.Subscribe(Key, EditingCallback);
        }

        private static void VerifyUnreleasedEditingSessionIsRetained()
        {
            Assert.That(Application.isPlaying, Is.True);
            Assert.That(EventBus.Instance, Is.SameAs(ownedBus),
                "Without Domain Reload or an explicit release, the registered instance must remain.");
            EventBus.Instance.Publish(Key);
            Assert.That(SessionState.GetInt(Prefix + "EditingCalls", -1), Is.EqualTo(1),
                "The editing listener is intentionally retained until the host explicitly closes it.");
        }

        private static void HostOpenPlaySession(int session, bool keepDomain)
        {
            Assert.That(Application.isPlaying, Is.True);
            if (keepDomain) AssertInvalid(previousBus);
            ownedBus = EventBus.Instance;
            if (keepDomain) Assert.That(ownedBus, Is.Not.SameAs(previousBus));
            ownedBus.Publish(Key);
            Assert.That(SessionState.GetInt(Prefix + "EditingCalls", -1), Is.EqualTo(keepDomain ? 1 : 0));
            Assert.That(SessionState.GetInt(Prefix + "Calls", -1), Is.EqualTo(session - 1));
            ownedBus.Subscribe(Key, PlayCallback);
            ownedBus.Publish(Key);
            Assert.That(SessionState.GetInt(Prefix + "Calls", -1), Is.EqualTo(session));
        }

        private static void HostCloseSession()
        {
            if (ownedBus == null) return;
            ownedBus.Unsubscribe(Key, EditingCallback);
            ownedBus.Unsubscribe(Key, PlayCallback);
            EventBus.ReleaseInstance();
            previousBus = ownedBus;
            ownedBus = null;
        }

        private static void VerifyExit(int session, bool keepDomain)
        {
            Assert.That(Application.isPlaying, Is.False);
            if (keepDomain) AssertInvalid(previousBus);
            Assert.That(SessionState.GetInt(Prefix + "Calls", -1), Is.EqualTo(session));
            Assert.That(SessionState.GetInt(Prefix + "EditingCalls", -1), Is.EqualTo(keepDomain ? 1 : 0));
        }

        private static void AssertInvalid(EventBus old)
        {
            Assert.That(old, Is.Not.Null);
            Assert.Throws<InvalidOperationException>(() => old.Publish(Key));
            Assert.Throws<InvalidOperationException>(() => old.Subscribe(Key, EditingCallback));
            Assert.DoesNotThrow(() => old.Unsubscribe(Key, EditingCallback));
            Assert.DoesNotThrow(() => old.Unsubscribe(Key, PlayCallback));
        }

        private static void PlayCallback()
        {
            SessionState.SetInt(Prefix + "Calls", SessionState.GetInt(Prefix + "Calls", 0) + 1);
        }
        private static void EditingCallback()
        {
            SessionState.SetInt(Prefix + "EditingCalls", SessionState.GetInt(Prefix + "EditingCalls", 0) + 1);
        }
    }
}
