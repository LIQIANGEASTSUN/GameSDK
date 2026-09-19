using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace GameSDK.Tests
{
    [TestFixture]
    [Parallelizable(ParallelScope.None)]
    public sealed class UtilsPathTests
    {
        [Test]
        public void CombinePathNormalizesSeparatorsAfterCombiningAllSegments()
        {
            Assert.That(UtilsPath.CombinePath(@"base\child", @"nested\file.txt"),
                Is.EqualTo("base/child/nested/file.txt"));
        }

        [Test]
        public void CombinePathAllowsZeroEmptyAndWhitespaceSegments()
        {
            Assert.That(UtilsPath.CombinePath(), Is.EqualTo(string.Empty));
            Assert.That(UtilsPath.CombinePath("", "base", "", "child", ""), Is.EqualTo("base/child"));
            Assert.That(UtilsPath.CombinePath("base", " ", "file.txt"), Is.EqualTo("base/ /file.txt"));
        }

        [Test]
        public void CombinePathPreservesUnicodeAndSpacesWithoutEncoding()
        {
            Assert.That(UtilsPath.CombinePath("根目录", "子 文件夹", "文档 🎮.json"),
                Is.EqualTo("根目录/子 文件夹/文档 🎮.json"));
        }

        [TestCase("jar:file:///data/app.apk!/assets")]
        [TestCase("http://example.com/assets")]
        [TestCase("https://example.com/assets")]
        public void CombinePathPreservesExistingUriPrefixes(string root)
        {
            Assert.That(UtilsPath.CombinePath(root, "子目录", "file name.json"),
                Is.EqualTo(root + "/子目录/file name.json"));
        }

        [Test]
        public void NativeRootedSegmentResetsEarlierSegments()
        {
            var root = Path.GetPathRoot(Path.GetTempPath());
            Assert.That(UtilsPath.CombinePath("discarded", root, "child.txt"),
                Is.EqualTo(root.Replace('\\', '/').TrimEnd('/') + "/child.txt"));
        }

        [Test]
        public void BackslashIsInterpretedByNativePathRulesBeforeNormalization()
        {
            var result = UtilsPath.CombinePath("base", @"\child");
            Assert.That(result, Is.EqualTo(Path.DirectorySeparatorChar == '\\' ? "/child" : "base//child"));
        }

        [Test]
        public void CombinePathRetainsStandardNullArgumentErrors()
        {
            Assert.Throws<ArgumentNullException>(() => UtilsPath.CombinePath((string[])null));
            Assert.Throws<ArgumentNullException>(() => UtilsPath.CombinePath("base", null));
        }

        [Test]
        [Category("UnityPath")]
        public void RootPropertiesReturnCurrentUnityValuesUnchanged()
        {
            Assert.That(UtilsPath.StreamingAssetsPath, Is.EqualTo(Application.streamingAssetsPath));
            Assert.That(UtilsPath.PersistentDataPath, Is.EqualTo(Application.persistentDataPath));
        }

        [Test]
        [Category("UnityPath")]
        public void RootMethodsWithNoArgumentsReturnNormalizedUnityRoots()
        {
            Assert.That(UtilsPath.GetStreamingAssetsFilePath(),
                Is.EqualTo(Application.streamingAssetsPath.Replace('\\', '/')));
            Assert.That(UtilsPath.GetPersistentDataPath(),
                Is.EqualTo(Application.persistentDataPath.Replace('\\', '/')));
        }

        [Test]
        [Category("UnityPath")]
        public void RootMethodsCombineMultipleRelativeSegmentsAndEmptySegments()
        {
            const string expectedSuffix = "/配置/子 目录/data.json";
            Assert.That(UtilsPath.GetStreamingAssetsFilePath("配置", "", "子 目录", "data.json"),
                Is.EqualTo(Application.streamingAssetsPath.Replace('\\', '/').TrimEnd('/') + expectedSuffix));
            Assert.That(UtilsPath.GetPersistentDataPath("配置", "", "子 目录", "data.json"),
                Is.EqualTo(Application.persistentDataPath.Replace('\\', '/').TrimEnd('/') + expectedSuffix));
        }

        [Test]
        [Category("UnityPath")]
        public void RootMethodsCombineBeforeNormalizingRelativeBackslashes()
        {
            var streaming = UtilsPath.GetStreamingAssetsFilePath(@"\relative", "child.txt");
            var persistent = UtilsPath.GetPersistentDataPath(@"\relative", "child.txt");
            if (Path.DirectorySeparatorChar == '\\')
            {
                Assert.That(streaming, Is.EqualTo("/relative/child.txt"));
                Assert.That(persistent, Is.EqualTo("/relative/child.txt"));
            }
            else
            {
                Assert.That(streaming,
                    Is.EqualTo(Application.streamingAssetsPath.Replace('\\', '/').TrimEnd('/') + "//relative/child.txt"));
                Assert.That(persistent,
                    Is.EqualTo(Application.persistentDataPath.Replace('\\', '/').TrimEnd('/') + "//relative/child.txt"));
            }
        }

        [Test]
        [Category("UnityPath")]
        public void RootMethodsRetainStandardNullArgumentErrors()
        {
            Assert.Throws<ArgumentNullException>(() => UtilsPath.GetStreamingAssetsFilePath((string[])null));
            Assert.Throws<ArgumentNullException>(() => UtilsPath.GetStreamingAssetsFilePath("child", null));
            Assert.Throws<ArgumentNullException>(() => UtilsPath.GetPersistentDataPath((string[])null));
            Assert.Throws<ArgumentNullException>(() => UtilsPath.GetPersistentDataPath("child", null));
        }
    }
}
