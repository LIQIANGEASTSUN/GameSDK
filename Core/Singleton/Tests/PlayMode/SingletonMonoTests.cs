using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace GameSDK.Tests
{
    public sealed class SingletonMonoTests
    {
        private readonly List<GameObject> hosts = new List<GameObject>();
        private readonly List<Scene> scenes = new List<Scene>();

        [SetUp]
        public void SetUp()
        {
            MonoSingletonProbe.ResetObservations();
            MonoSingletonProbe.ReleaseInstance();
            OtherMonoSingletonProbe.ReleaseInstance();
            MonoSingletonProbe.ResetObservations();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            MonoSingletonProbe.Initializing = null;
            MonoSingletonProbe.Releasing = null;
            MonoSingletonProbe.Awaking = null;
            MonoSingletonProbe.Enabling = null;
            MonoSingletonProbe.ReleaseInstance();
            OtherMonoSingletonProbe.ReleaseInstance();
            foreach (GameObject host in hosts)
            {
                if (host != null)
                    Object.Destroy(host);
            }
            hosts.Clear();
            foreach (Scene scene in scenes)
            {
                if (scene.IsValid() && scene.isLoaded)
                    yield return SceneManager.UnloadSceneAsync(scene);
            }
            scenes.Clear();
            yield return null;
            MonoSingletonProbe.ResetObservations();
        }

        [Test]
        public void InstanceCreatesOneInitializedHostAndTypesRemainIndependent()
        {
            MonoSingletonProbe first = MonoSingletonProbe.Instance;
            OtherMonoSingletonProbe other = OtherMonoSingletonProbe.Instance;
            Assert.That(MonoSingletonProbe.Instance, Is.SameAs(first));
            Assert.That(first.InitializeCalls, Is.EqualTo(1));
            Assert.That(first.HasResource, Is.True);
            Assert.That(first.gameObject.activeSelf, Is.True);
            Assert.That(first.gameObject.GetComponents<MonoSingletonProbe>(), Has.Length.EqualTo(1));
            Assert.That(other.InitializeCalls, Is.EqualTo(1));

            MonoSingletonProbe.ReleaseInstance();
            Assert.That(first.ReleaseCalls, Is.EqualTo(1));
            Assert.That(first.HasResource, Is.False);
            Assert.That(OtherMonoSingletonProbe.Instance, Is.SameAs(other));
            Assert.That(other.ReleaseCalls, Is.Zero);
        }

        [UnityTest]
        public IEnumerator ExternalComponentsStayUnmanagedAndDoNotAffectTheFrameworkInstance()
        {
            GameObject activeHost = NewHost(true);
            BoxCollider activeMarker = activeHost.AddComponent<BoxCollider>();
            MonoSingletonProbe active = activeHost.AddComponent<MonoSingletonProbe>();
            GameObject parent = NewHost(false);
            GameObject inactiveHost = NewHost(false);
            inactiveHost.transform.SetParent(parent.transform);
            inactiveHost.hideFlags = HideFlags.HideInHierarchy | HideFlags.HideInInspector;
            BoxCollider inactiveMarker = inactiveHost.AddComponent<BoxCollider>();
            MonoSingletonProbe inactive = inactiveHost.AddComponent<MonoSingletonProbe>();
            GameObject subtypeHost = NewHost(true);
            MonoSingletonProbe subtype = subtypeHost.AddComponent<MonoSingletonProbe.WrongSubtype>();
            GameObject invalidHost = NewHost(true);
            PublicConstructorMonoProbe invalid = invalidHost.AddComponent<PublicConstructorMonoProbe>();

            MonoSingletonProbe.ReleaseInstance();
            PublicConstructorMonoProbe.ReleaseInstance();
            Assert.That(MonoSingletonProbe.InitializeCount, Is.Zero);
            Assert.That(MonoSingletonProbe.ReleaseCount, Is.Zero);
            Assert.That(invalid.InitializeCalls + invalid.ReleaseCalls, Is.Zero);

            MonoSingletonProbe managed = MonoSingletonProbe.Instance;
            GameObject managedHost = managed.gameObject;
            Assert.That(managed, Is.Not.SameAs(active));
            Assert.That(managed, Is.Not.SameAs(inactive));
            Assert.That(managed, Is.Not.SameAs(subtype));
            Assert.That(managed.GetType(), Is.EqualTo(typeof(MonoSingletonProbe)));
            Assert.That(MonoSingletonProbe.InitializeCount, Is.EqualTo(1));
            Assert.That(active.InitializeCalls + inactive.InitializeCalls + subtype.InitializeCalls, Is.Zero);
            Assert.That(active != null && inactive != null && subtype != null && invalid != null, Is.True);
            Assert.That(inactiveHost.activeSelf || parent.activeSelf, Is.False);
            Assert.That(inactiveHost.transform.parent, Is.SameAs(parent.transform));

            Object.DestroyImmediate(active);
            Assert.That(active.ReleaseCalls, Is.Zero);
            Assert.That(activeHost != null && activeMarker != null, Is.True);
            Assert.That(MonoSingletonProbe.Instance, Is.SameAs(managed));
            Assert.That(managed.HasResource, Is.True);
            Assert.That(managed.ReleaseCalls, Is.Zero);
            Assert.That(managedHost != null, Is.True);

            MonoSingletonProbe.ReleaseInstance();
            yield return null;

            Assert.That(managedHost == null, Is.True);
            Assert.That(activeHost != null && activeMarker != null, Is.True);
            Assert.That(inactive != null && subtype != null && invalid != null, Is.True);
            Assert.That(inactiveHost != null && inactiveMarker != null && parent != null, Is.True);
            Assert.That(subtypeHost != null && invalidHost != null, Is.True);
            Assert.That(inactive.InitializeCalls + subtype.InitializeCalls, Is.Zero);
            Assert.That(inactive.ReleaseCalls + subtype.ReleaseCalls, Is.Zero);
            Assert.That(invalid.InitializeCalls + invalid.ReleaseCalls, Is.Zero);
        }

        [Test]
        public void InvalidTypeContractsFailBeforeCreatingUnityObjects()
        {
            Assert.Throws<InvalidOperationException>(() => { _ = PublicConstructorMonoProbe.Instance; });
            Assert.Throws<InvalidOperationException>(() => { _ = PublicOverloadMonoProbe.Instance; });
            Assert.Throws<InvalidOperationException>(() => { _ = ProtectedConstructorMonoProbe.Instance; });
            Assert.Throws<InvalidOperationException>(() => { _ = AbstractMonoProbe.Instance; });
            Assert.That(Resources.FindObjectsOfTypeAll<PublicConstructorMonoProbe>(), Is.Empty);
            Assert.That(Resources.FindObjectsOfTypeAll<PublicOverloadMonoProbe>(), Is.Empty);
            Assert.That(Resources.FindObjectsOfTypeAll<ProtectedConstructorMonoProbe>(), Is.Empty);
            Assert.That(Resources.FindObjectsOfTypeAll<AbstractMonoProbe>(), Is.Empty);
            Assert.Throws<InvalidOperationException>(() => { _ = PublicConstructorMonoProbe.Instance; });
        }

        [Test]
        public void InitializationCompletesBeforeActivationAndRunsOnlyOnce()
        {
            var callbacks = new List<string>();
            MonoSingletonProbe.Initializing = candidate =>
            {
                Assert.That(candidate.gameObject.activeSelf, Is.False);
                Assert.That(candidate.HasResource, Is.True);
                Assert.That(candidate.InitializeCalls, Is.EqualTo(1));
                callbacks.Add("initialize");
            };
            MonoSingletonProbe.Awaking = candidate =>
            {
                Assert.That(candidate.gameObject.activeSelf, Is.True);
                Assert.That(candidate.HasResource, Is.True);
                Assert.That(candidate.InitializeCalls, Is.EqualTo(1));
                callbacks.Add("awake");
            };
            MonoSingletonProbe.Enabling = candidate =>
            {
                Assert.That(candidate.gameObject.activeSelf, Is.True);
                Assert.That(candidate.HasResource, Is.True);
                Assert.That(candidate.InitializeCalls, Is.EqualTo(1));
                Assert.That(candidate.ReleaseCalls, Is.Zero);
                callbacks.Add("enable");
            };

            MonoSingletonProbe ready = MonoSingletonProbe.Instance;
            callbacks.Add("returned");
            CollectionAssert.AreEqual(new[] { "initialize", "awake", "enable", "returned" }, callbacks);
            Assert.That(ready.InitializeCalls, Is.EqualTo(1));
            Assert.That(MonoSingletonProbe.Instance, Is.SameAs(ready));
        }

        [Test]
        public void RepeatedReleaseCleansOnceWithoutCreatingAnotherInstance()
        {
            MonoSingletonProbe current = MonoSingletonProbe.Instance;
            int releaseChecks = 0;
            MonoSingletonProbe.Releasing = candidate =>
            {
                Assert.That(candidate, Is.SameAs(current));
                Assert.That(candidate.HasResource, Is.False);
                Assert.That(candidate.ReleaseCalls, Is.EqualTo(1));
                releaseChecks++;
            };

            MonoSingletonProbe.ReleaseInstance();
            MonoSingletonProbe.ReleaseInstance();
            Assert.That(releaseChecks, Is.EqualTo(1));
            Assert.That(current.ReleaseCalls, Is.EqualTo(1));
            Assert.That(current.HasResource, Is.False);
            Assert.That(MonoSingletonProbe.InitializeCount, Is.EqualTo(1));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void ExternalComponentsAddedDuringInitializationOrReleaseStayUnmanaged(bool duringInitialization)
        {
            MonoSingletonProbe external = null;
            Action<MonoSingletonProbe> addExternal = candidate =>
                external = NewHost(true).AddComponent<MonoSingletonProbe>();
            if (duringInitialization)
                MonoSingletonProbe.Initializing = addExternal;
            else
                MonoSingletonProbe.Releasing = addExternal;

            MonoSingletonProbe current = MonoSingletonProbe.Instance;
            if (!duringInitialization)
                MonoSingletonProbe.ReleaseInstance();
            Assert.That(external, Is.Not.Null);
            Assert.That(external.InitializeCalls, Is.Zero);
            Assert.That(external.ReleaseCalls, Is.Zero);
            Assert.That(current.InitializeCalls, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator InitializationFailureCleansOncePreservesOriginalAndRetries()
        {
            var original = new InvalidOperationException("initialization failure");
            GameObject failedHost = null;
            MonoSingletonProbe.Initializing = candidate =>
            {
                failedHost = candidate.gameObject;
                throw original;
            };
            Assert.That(Assert.Throws<InvalidOperationException>(() => { _ = MonoSingletonProbe.Instance; }), Is.SameAs(original));
            MonoSingletonProbe failed = MonoSingletonProbe.LastInitialized;
            Assert.That(failed.ReleaseCalls, Is.EqualTo(1));
            Assert.That(failed.HasResource, Is.False);
            MonoSingletonProbe.Initializing = null;
            MonoSingletonProbe replacement = MonoSingletonProbe.Instance;
            Assert.That(replacement, Is.Not.SameAs(failed));
            yield return null;
            Assert.That(failedHost == null, Is.True);
            Assert.That(failed.ReleaseCalls, Is.EqualTo(1));
            Assert.That(MonoSingletonProbe.Instance, Is.SameAs(replacement));
        }

        [UnityTest]
        public IEnumerator InitializationAndCleanupErrorsAreKeptInOrderAndFailedHostIsRemoved()
        {
            var original = new InvalidOperationException("initialization failure");
            var cleanup = new ArgumentException("cleanup failure");
            GameObject failedHost = null;
            MonoSingletonProbe.Initializing = candidate =>
            {
                failedHost = candidate.gameObject;
                throw original;
            };
            MonoSingletonProbe.Releasing = candidate => throw cleanup;

            AggregateException error = Assert.Throws<AggregateException>(() => { _ = MonoSingletonProbe.Instance; });
            Assert.That(error.InnerExceptions, Has.Count.EqualTo(2));
            Assert.That(error.InnerExceptions[0], Is.SameAs(original));
            Assert.That(error.InnerExceptions[1], Is.SameAs(cleanup));
            MonoSingletonProbe failed = MonoSingletonProbe.LastInitialized;
            Assert.That(failed.HasResource, Is.False);
            Assert.That(failed.ReleaseCalls, Is.EqualTo(1));
            Assert.DoesNotThrow(MonoSingletonProbe.ReleaseInstance);

            MonoSingletonProbe.Initializing = null;
            MonoSingletonProbe.Releasing = null;
            MonoSingletonProbe replacement = MonoSingletonProbe.Instance;
            Assert.That(replacement, Is.Not.SameAs(failed));
            yield return null;
            Assert.That(failedHost == null, Is.True);
            Assert.That(failed.ReleaseCalls, Is.EqualTo(1));
            Assert.That(MonoSingletonProbe.Instance, Is.SameAs(replacement));
        }

        [UnityTest]
        public IEnumerator ExplicitReleaseFailureStillDestroysOwnedHostAndPermitsRetry()
        {
            MonoSingletonProbe current = MonoSingletonProbe.Instance;
            GameObject ownedHost = current.gameObject;
            var original = new InvalidOperationException("release failure");
            MonoSingletonProbe.Releasing = candidate => throw original;
            Assert.That(Assert.Throws<InvalidOperationException>(MonoSingletonProbe.ReleaseInstance), Is.SameAs(original));
            Assert.DoesNotThrow(MonoSingletonProbe.ReleaseInstance);
            Assert.That(current.ReleaseCalls, Is.EqualTo(1));
            MonoSingletonProbe.Releasing = null;
            MonoSingletonProbe replacement = MonoSingletonProbe.Instance;
            yield return null;
            Assert.That(ownedHost == null, Is.True);
            Assert.That(current.ReleaseCalls, Is.EqualTo(1));
            Assert.That(MonoSingletonProbe.Instance, Is.SameAs(replacement));
        }

        [UnityTest]
        public IEnumerator ReleaseHookCanDestroyItsComponentWithoutRepeatingCleanupOrLeakingHost()
        {
            MonoSingletonProbe current = MonoSingletonProbe.Instance;
            GameObject ownedHost = current.gameObject;
            int cleanupChecks = 0;
            MonoSingletonProbe.Releasing = candidate =>
            {
                Assert.That(candidate, Is.SameAs(current));
                Assert.That(candidate.ReleaseCalls, Is.EqualTo(1));
                Object.DestroyImmediate(candidate);
                Assert.That(candidate.ReleaseCalls, Is.EqualTo(1));
                cleanupChecks++;
            };

            Assert.DoesNotThrow(MonoSingletonProbe.ReleaseInstance);
            Assert.That(cleanupChecks, Is.EqualTo(1));
            Assert.That(current.ReleaseCalls, Is.EqualTo(1));
            Assert.That(current.HasResource, Is.False);
            MonoSingletonProbe.Releasing = null;
            MonoSingletonProbe replacement = MonoSingletonProbe.Instance;
            Assert.That(replacement, Is.Not.SameAs(current));
            yield return null;
            Assert.That(ownedHost == null, Is.True);
            Assert.That(current.ReleaseCalls, Is.EqualTo(1));
            Assert.That(MonoSingletonProbe.Instance, Is.SameAs(replacement));
            Assert.That(replacement.HasResource, Is.True);
            Assert.That(replacement.ReleaseCalls, Is.Zero);
        }

        [UnityTest]
        public IEnumerator SynchronousLossDuringInitializationCleansWithoutOverlappingHooks()
        {
            GameObject invalidHost = null;
            bool insideInitialization = false;
            MonoSingletonProbe.Initializing = candidate =>
            {
                invalidHost = candidate.gameObject;
                insideInitialization = true;
                Object.DestroyImmediate(candidate);
                Assert.That(candidate.ReleaseCalls, Is.Zero);
                insideInitialization = false;
            };
            MonoSingletonProbe.Releasing = candidate => Assert.That(insideInitialization, Is.False);

            Assert.Throws<InvalidOperationException>(() => { _ = MonoSingletonProbe.Instance; });
            MonoSingletonProbe failed = MonoSingletonProbe.LastInitialized;
            Assert.That(failed.ReleaseCalls, Is.EqualTo(1));
            Assert.That(failed.HasResource, Is.False);
            MonoSingletonProbe.Initializing = null;
            MonoSingletonProbe.Releasing = null;
            MonoSingletonProbe replacement = MonoSingletonProbe.Instance;
            yield return null;
            Assert.That(invalidHost == null, Is.True);
            Assert.That(MonoSingletonProbe.Instance, Is.SameAs(replacement));
        }

        [UnityTest]
        public IEnumerator SynchronousLossDuringActivationDoesNotPublishAndRemovesOwnedHost()
        {
            GameObject invalidHost = null;
            bool insideActivation = false;
            MonoSingletonProbe.Enabling = candidate =>
            {
                invalidHost = candidate.gameObject;
                insideActivation = true;
                Object.DestroyImmediate(candidate);
                Assert.That(candidate.ReleaseCalls, Is.Zero);
                insideActivation = false;
            };
            MonoSingletonProbe.Releasing = candidate => Assert.That(insideActivation, Is.False);
            Assert.Throws<InvalidOperationException>(() => { _ = MonoSingletonProbe.Instance; });
            MonoSingletonProbe failed = MonoSingletonProbe.LastInitialized;
            Assert.That(failed.ReleaseCalls, Is.EqualTo(1));
            MonoSingletonProbe.Enabling = null;
            MonoSingletonProbe.Releasing = null;
            MonoSingletonProbe replacement = MonoSingletonProbe.Instance;
            yield return null;
            Assert.That(invalidHost == null, Is.True);
            Assert.That(failed.ReleaseCalls, Is.EqualTo(1));
            Assert.That(MonoSingletonProbe.Instance, Is.SameAs(replacement));
        }

        [UnityTest]
        public IEnumerator InactiveParentPreventsPublicationAndFailedChildIsCleanedWithoutRemovingParent()
        {
            GameObject parent = NewHost(false);
            GameObject failedHost = null;
            MonoSingletonProbe.Initializing = candidate =>
            {
                failedHost = candidate.gameObject;
                candidate.transform.SetParent(parent.transform);
            };

            Assert.Throws<InvalidOperationException>(() => { _ = MonoSingletonProbe.Instance; });
            MonoSingletonProbe failed = MonoSingletonProbe.LastInitialized;
            Assert.That(failed.InitializeCalls, Is.EqualTo(1));
            Assert.That(failed.ReleaseCalls, Is.EqualTo(1));
            Assert.That(failed.HasResource, Is.False);
            MonoSingletonProbe.Initializing = null;
            MonoSingletonProbe replacement = MonoSingletonProbe.Instance;
            Assert.That(replacement, Is.Not.SameAs(failed));
            Assert.That(replacement.gameObject.activeInHierarchy, Is.True);
            yield return null;

            Assert.That(failedHost == null, Is.True);
            Assert.That(parent != null, Is.True);
            Assert.That(parent.activeSelf, Is.False);
            Assert.That(failed.ReleaseCalls, Is.EqualTo(1));
            Assert.That(MonoSingletonProbe.Instance, Is.SameAs(replacement));
            Assert.That(replacement.HasResource, Is.True);
            Assert.That(replacement.ReleaseCalls, Is.Zero);
        }

        [UnityTest]
        public IEnumerator DisablingDuringOnEnablePreventsPublicationAndAllowsSameFrameRetry()
        {
            GameObject failedHost = null;
            MonoSingletonProbe.Enabling = candidate =>
            {
                failedHost = candidate.gameObject;
                candidate.gameObject.SetActive(false);
            };

            Assert.Throws<InvalidOperationException>(() => { _ = MonoSingletonProbe.Instance; });
            MonoSingletonProbe failed = MonoSingletonProbe.LastInitialized;
            Assert.That(failed.InitializeCalls, Is.EqualTo(1));
            Assert.That(failed.ReleaseCalls, Is.EqualTo(1));
            Assert.That(failed.HasResource, Is.False);
            MonoSingletonProbe.Enabling = null;
            MonoSingletonProbe replacement = MonoSingletonProbe.Instance;
            Assert.That(replacement, Is.Not.SameAs(failed));
            Assert.That(replacement.gameObject.activeInHierarchy, Is.True);
            yield return null;

            Assert.That(failedHost == null, Is.True);
            Assert.That(failed.ReleaseCalls, Is.EqualTo(1));
            Assert.That(MonoSingletonProbe.Instance, Is.SameAs(replacement));
            Assert.That(replacement.HasResource, Is.True);
            Assert.That(replacement.ReleaseCalls, Is.Zero);
        }

        [UnityTest]
        public IEnumerator ExternalDeferredDestructionIsObservedAfterUnityActuallyDestroys()
        {
            MonoSingletonProbe current = MonoSingletonProbe.Instance;
            GameObject host = current.gameObject;
            Object.Destroy(host);
            Assert.That(MonoSingletonProbe.Instance, Is.SameAs(current));
            Assert.That(current.ReleaseCalls, Is.Zero);
            yield return null;
            Assert.That(host == null, Is.True);
            Assert.That(current.ReleaseCalls, Is.EqualTo(1));
            Assert.That(current.HasResource, Is.False);
            Assert.That(MonoSingletonProbe.Instance, Is.Not.SameAs(current));
        }

        [UnityTest]
        public IEnumerator ExternalComponentDestructionAlsoRemovesFrameworkOwnedHost()
        {
            MonoSingletonProbe current = MonoSingletonProbe.Instance;
            GameObject host = current.gameObject;
            Object.Destroy(current);
            yield return null;
            Assert.That(current.ReleaseCalls, Is.EqualTo(1));
            yield return null;
            Assert.That(host == null, Is.True);
            Assert.That(MonoSingletonProbe.Instance, Is.Not.SameAs(current));
        }

        [UnityTest]
        public IEnumerator OnDestroyCleanupFailureIsLoggedOnceAndDoesNotBlockReplacement()
        {
            MonoSingletonProbe current = MonoSingletonProbe.Instance;
            MonoSingletonProbe.Releasing = candidate => throw new InvalidOperationException("destroy cleanup failure");
            LogAssert.Expect(LogType.Exception, new Regex("InvalidOperationException: destroy cleanup failure"));
            Object.Destroy(current.gameObject);
            yield return null;
            Assert.That(current.ReleaseCalls, Is.EqualTo(1));
            Assert.That(current.HasResource, Is.False);
            MonoSingletonProbe.Releasing = null;
            Assert.That(MonoSingletonProbe.Instance, Is.Not.SameAs(current));
        }

        [UnityTest]
        public IEnumerator SameFrameReleaseAndRecreateSurvivesOldDestroyCallback()
        {
            Assert.DoesNotThrow(MonoSingletonProbe.ReleaseInstance);
            Assert.That(MonoSingletonProbe.InitializeCount, Is.Zero);
            MonoSingletonProbe old = MonoSingletonProbe.Instance;
            GameObject oldHost = old.gameObject;
            MonoSingletonProbe.ReleaseInstance();
            MonoSingletonProbe.ReleaseInstance();
            Assert.That(old.ReleaseCalls, Is.EqualTo(1));
            MonoSingletonProbe replacement = MonoSingletonProbe.Instance;
            Assert.That(replacement, Is.Not.SameAs(old));
            yield return null;
            Assert.That(oldHost == null, Is.True);
            Assert.That(old.ReleaseCalls, Is.EqualTo(1));
            Assert.That(MonoSingletonProbe.Instance, Is.SameAs(replacement));
            Assert.That(replacement.HasResource, Is.True);
            Assert.That(replacement.ReleaseCalls, Is.Zero);
        }

        [UnityTest]
        public IEnumerator ReleasedComponentCannotRegisterWhenReactivatedBeforeDestroy()
        {
            MonoSingletonProbe old = MonoSingletonProbe.Instance;
            GameObject oldHost = old.gameObject;
            oldHost.SetActive(false);
            MonoSingletonProbe.ReleaseInstance();
            MonoSingletonProbe replacement = MonoSingletonProbe.Instance;
            oldHost.SetActive(true);
            Assert.That(old.InitializeCalls, Is.EqualTo(1));
            Assert.That(old.ReleaseCalls, Is.EqualTo(1));
            Assert.That(MonoSingletonProbe.Instance, Is.SameAs(replacement));
            yield return null;
            Assert.That(old == null, Is.True);
            Assert.That(oldHost == null, Is.True);
            Assert.That(MonoSingletonProbe.Instance, Is.SameAs(replacement));
        }

        [UnityTest]
        public IEnumerator DisablingDoesNotReleaseAndSceneUnloadDoes()
        {
            Scene scene = NewScene();
            MonoSingletonProbe current = MonoSingletonProbe.Instance;
            GameObject host = current.gameObject;
            SceneManager.MoveGameObjectToScene(host, scene);
            current.enabled = false;
            host.SetActive(false);
            Assert.That(MonoSingletonProbe.Instance, Is.SameAs(current));
            Assert.That(current.ReleaseCalls, Is.Zero);
            yield return SceneManager.UnloadSceneAsync(scene);
            Assert.That(host == null, Is.True);
            Assert.That(current.ReleaseCalls, Is.EqualTo(1));
            Assert.That(MonoSingletonProbe.Instance, Is.Not.SameAs(current));
        }

        [UnityTest]
        public IEnumerator FrameworkCreatedHostFollowsItsSceneUnlessUserPreservesIt()
        {
            Scene scene = NewScene();
            Scene previous = SceneManager.GetActiveScene();
            MonoSingletonProbe created;
            try
            {
                Assert.That(SceneManager.SetActiveScene(scene), Is.True);
                created = MonoSingletonProbe.Instance;
                Assert.That(created.gameObject.scene, Is.EqualTo(scene));
            }
            finally
            {
                SceneManager.SetActiveScene(previous);
            }

            yield return SceneManager.UnloadSceneAsync(scene);
            Assert.That(created == null, Is.True);
            Assert.That(created.ReleaseCalls, Is.EqualTo(1));
            Assert.That(MonoSingletonProbe.Instance, Is.Not.SameAs(created));
        }

        [UnityTest]
        public IEnumerator UserCanPreserveFrameworkHostAcrossSceneUnloadUntilExplicitRelease()
        {
            Scene scene = NewScene();
            MonoSingletonProbe current = MonoSingletonProbe.Instance;
            GameObject host = current.gameObject;
            SceneManager.MoveGameObjectToScene(host, scene);
            Object.DontDestroyOnLoad(host);
            yield return SceneManager.UnloadSceneAsync(scene);
            Assert.That(host != null, Is.True);
            Assert.That(current.ReleaseCalls, Is.Zero);
            Assert.That(MonoSingletonProbe.Instance, Is.SameAs(current));
            MonoSingletonProbe.ReleaseInstance();
            yield return null;
            Assert.That(current == null, Is.True);
            Assert.That(current.ReleaseCalls, Is.EqualTo(1));
            Assert.That(host == null, Is.True);
        }

        private GameObject NewHost(bool active)
        {
            var host = new GameObject("Singleton test host");
            host.SetActive(active);
            hosts.Add(host);
            return host;
        }

        private Scene NewScene()
        {
            Scene scene = SceneManager.CreateScene("Singleton test " + Guid.NewGuid().ToString("N"));
            scenes.Add(scene);
            return scene;
        }
    }
}
