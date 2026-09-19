using System.Collections;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace GameSDK
{
    public sealed class SingletonMonoEditorTests
    {
        private Scene previewScene;

        [SetUp]
        public void SetUp()
        {
            Assert.That(Application.isPlaying, Is.False);
            SingletonMonoEditWorldProbe.ReleaseInstance();
            SingletonMonoEditWorldProbe.ResetObservations();
            previewScene = EditorSceneManager.NewPreviewScene();
        }

        [TearDown]
        public void TearDown()
        {
            try
            {
                SingletonMonoEditWorldProbe.ReleaseInstance();
            }
            finally
            {
                if (previewScene.IsValid())
                    EditorSceneManager.ClosePreviewScene(previewScene);
            }
        }

        [UnityTest]
        public IEnumerator ExecuteAlwaysEditObjectsRemainUnmanaged()
        {
            var firstHost = CreateHost("Singleton edit world first");
            var secondHost = CreateHost("Singleton edit world second");
            var first = firstHost.AddComponent<SingletonMonoEditWorldProbe>();
            var second = secondHost.AddComponent<SingletonMonoEditWorldProbe>();
            yield return null;

            Assert.That(Application.IsPlaying(firstHost), Is.False);
            Assert.That(Application.IsPlaying(secondHost), Is.False);
            Assert.That(SingletonMonoEditWorldProbe.EnableCount, Is.EqualTo(2),
                "The ExecuteAlways components must receive real editing-world lifecycle callbacks.");
            Assert.That(first != null && second != null, Is.True,
                "Both externally created editing-world components must remain intact.");
            Assert.That(SingletonMonoEditWorldProbe.InitializeCount, Is.Zero);
            Assert.That(SingletonMonoEditWorldProbe.ReleaseCount, Is.Zero);

            SingletonMonoEditWorldProbe.ReleaseInstance();
            SingletonMonoEditWorldProbe.ReleaseInstance();
            yield return null;

            Assert.That(first != null && second != null, Is.True,
                "ReleaseInstance must not destroy an externally created editing-world component.");
            Assert.That(firstHost.GetComponent<SingletonMonoEditWorldProbe>(), Is.SameAs(first));
            Assert.That(secondHost.GetComponent<SingletonMonoEditWorldProbe>(), Is.SameAs(second));
            Assert.That(SingletonMonoEditWorldProbe.InitializeCount, Is.Zero);
            Assert.That(SingletonMonoEditWorldProbe.ReleaseCount, Is.Zero);
        }

        private GameObject CreateHost(string name)
        {
            var host = new GameObject(name);
            SceneManager.MoveGameObjectToScene(host, previewScene);
            return host;
        }
    }
}
