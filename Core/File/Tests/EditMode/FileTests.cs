using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using NUnit.Framework;

namespace GameSDK.Tests
{
    [TestFixture]
    [Parallelizable(ParallelScope.None)]
    public sealed class FileTests
    {
        private string temporaryDirectory;

        [SetUp]
        public void SetUp()
        {
            temporaryDirectory = Path.Combine(Path.GetTempPath(), "GameSDK.File.Tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(temporaryDirectory);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(temporaryDirectory))
                Directory.Delete(temporaryDirectory, true);
        }

        [Test]
        public void TextRoundTripsChineseWithoutBomAndSupportsOverwriteAndAppend()
        {
            var path = InTemporaryDirectory("nested", "文本.txt");
            FileWrite.WriteText(path, "中文与 emoji 🎮");
            Assert.That(FileRead.ReadText(path), Is.EqualTo("中文与 emoji 🎮"));
            Assert.That(File.ReadAllBytes(path), Is.EqualTo(new UTF8Encoding(false).GetBytes("中文与 emoji 🎮")));

            FileWrite.WriteText(path, "替换");
            FileWrite.WriteText(path, "追加", true);
            Assert.That(FileRead.ReadText(path), Is.EqualTo("替换追加"));
            AssertCanOpenExclusively(path);
        }

        [Test]
        public void BytesRoundTripAllValuesAndSupportOverwriteAndAppend()
        {
            var path = InTemporaryDirectory("nested", "bytes.bin");
            var data = new byte[16387];
            for (var index = 0; index < data.Length; index++)
                data[index] = (byte)index;

            FileWrite.WriteBytes(path, data);
            Assert.That(FileRead.ReadBytes(path), Is.EqualTo(data));
            FileWrite.WriteBytes(path, new byte[] { 0, 255 });
            FileWrite.WriteBytes(path, new byte[] { 128, 10 }, true);
            Assert.That(FileRead.ReadBytes(path), Is.EqualTo(new byte[] { 0, 255, 128, 10 }));
            AssertCanOpenExclusively(path);
        }

        [Test]
        public void LinesUsePlatformTerminatorsAndTreatNullAsEmptyLine()
        {
            var path = InTemporaryDirectory("nested", "lines.txt");
            FileWrite.WriteLines(path, new[] { "中文", null, "末行" });
            var expected = "中文" + Environment.NewLine + Environment.NewLine + "末行" + Environment.NewLine;
            Assert.That(File.ReadAllBytes(path), Is.EqualTo(new UTF8Encoding(false).GetBytes(expected)));
            Assert.That(FileRead.ReadLines(path), Is.EqualTo(new[] { "中文", "", "末行" }));

            FileWrite.WriteLines(path, new[] { "替换" });
            FileWrite.WriteLines(path, new[] { "追加" }, true);
            Assert.That(FileRead.ReadLines(path), Is.EqualTo(new[] { "替换", "追加" }));
            AssertCanOpenExclusively(path);
        }

        [Test]
        public void AppendingLinesDoesNotRepairMissingExistingNewline()
        {
            var path = InTemporaryDirectory("lines.txt");
            FileWrite.WriteText(path, "已有");
            FileWrite.WriteLines(path, new[] { "首行", "次行" }, true);
            Assert.That(FileRead.ReadLines(path), Is.EqualTo(new[] { "已有首行", "次行" }));
        }

        [TestCase("text")]
        [TestCase("bytes")]
        [TestCase("lines")]
        public void EmptyContentCreatesAndOverwritesEmptyFiles(string kind)
        {
            var path = InTemporaryDirectory("empty.txt");
            WriteEmpty(kind, path, false);
            Assert.That(File.Exists(path), Is.True);
            Assert.That(FileRead.ReadText(path), Is.Empty);
            Assert.That(FileRead.ReadBytes(path), Is.Empty);
            Assert.That(FileRead.ReadLines(path), Is.Empty);

            File.WriteAllText(path, "old");
            WriteEmpty(kind, path, true);
            Assert.That(FileRead.ReadText(path), Is.EqualTo("old"));
            WriteEmpty(kind, path, false);
            Assert.That(new FileInfo(path).Length, Is.Zero);
        }

        [TestCase("text")]
        [TestCase("bytes")]
        [TestCase("lines")]
        public void AppendCanCreateMissingFile(string kind)
        {
            var path = InTemporaryDirectory("new", "empty.txt");
            WriteEmpty(kind, path, true);
            Assert.That(File.Exists(path), Is.True);
            Assert.That(new FileInfo(path).Length, Is.Zero);
        }

        [TestCase("utf8")]
        [TestCase("utf16")]
        public void TextAndLinesDetectUnicodeBom(string encodingName)
        {
            var path = InTemporaryDirectory("bom.txt");
            var encoding = encodingName == "utf8" ? (Encoding)new UTF8Encoding(true) : Encoding.Unicode;
            File.WriteAllText(path, "首行\r\n次行\n", encoding);
            Assert.That(FileRead.ReadText(path), Is.EqualTo("首行\r\n次行\n"));
            Assert.That(FileRead.ReadLines(path), Is.EqualTo(new[] { "首行", "次行" }));
        }

        [Test]
        public void InvalidUnicodeUsesReplacementPolicy()
        {
            var path = InTemporaryDirectory("replacement.txt");
            FileWrite.WriteText(path, "\ud800");
            Assert.That(FileRead.ReadText(path), Is.EqualTo("\ufffd"));
            FileWrite.WriteBytes(path, new byte[] { 0xff });
            Assert.That(FileRead.ReadText(path), Is.EqualTo("\ufffd"));
        }

        [Test]
        public void CreateProtectsExistingFileUnlessOverwriteIsExplicit()
        {
            var path = InTemporaryDirectory("new", "created.txt");
            FileWrite.Create(path);
            Assert.That(File.Exists(path), Is.True);
            Assert.That(new FileInfo(path).Length, Is.Zero);
            File.WriteAllText(path, "keep");

            Assert.Throws<IOException>(() => FileWrite.Create(path));
            Assert.That(File.ReadAllText(path), Is.EqualTo("keep"));
            AssertCanOpenExclusively(path);
            FileWrite.Create(path, true);
            Assert.That(new FileInfo(path).Length, Is.Zero);
            AssertCanOpenExclusively(path);
        }

        [Test]
        public void ClearKeepsExistingFileAndNeverCreatesMissingFileOrParent()
        {
            var existing = InTemporaryDirectory("existing.txt");
            File.WriteAllText(existing, "old");
            FileWrite.Clear(existing);
            Assert.That(File.Exists(existing), Is.True);
            Assert.That(new FileInfo(existing).Length, Is.Zero);
            AssertCanOpenExclusively(existing);

            var missing = InTemporaryDirectory("missing.txt");
            Assert.Catch<IOException>(() => FileWrite.Clear(missing));
            Assert.That(File.Exists(missing), Is.False);
            var missingParent = InTemporaryDirectory("missing", "file.txt");
            Assert.Catch<IOException>(() => FileWrite.Clear(missingParent));
            Assert.That(Directory.Exists(Path.GetDirectoryName(missingParent)), Is.False);
        }

        [Test]
        public void PlainRelativeFileNamesSupportAllReadWriteOperations()
        {
            var previousDirectory = Directory.GetCurrentDirectory();
            try
            {
                Directory.SetCurrentDirectory(temporaryDirectory);
                FileWrite.Create("created.txt");
                FileWrite.WriteText("text.txt", "文本");
                FileWrite.WriteBytes("bytes.bin", new byte[] { 1, 2 });
                FileWrite.WriteLines("lines.txt", new[] { "line" });
                Assert.That(FileRead.ReadText("text.txt"), Is.EqualTo("文本"));
                Assert.That(FileRead.ReadBytes("bytes.bin"), Is.EqualTo(new byte[] { 1, 2 }));
                Assert.That(FileRead.ReadLines("lines.txt"), Is.EqualTo(new[] { "line" }));
                FileWrite.Clear("text.txt");
                Assert.That(FileRead.ReadText("text.txt"), Is.Empty);
            }
            finally
            {
                Directory.SetCurrentDirectory(previousDirectory);
            }
        }

        [TestCase("text")]
        [TestCase("bytes")]
        [TestCase("lines")]
        public void NullDataNeitherTruncatesExistingFileNorCreatesParents(string kind)
        {
            var existing = InTemporaryDirectory("existing.txt");
            File.WriteAllText(existing, "keep");
            Assert.Throws<ArgumentNullException>(() => WriteNull(kind, existing));
            Assert.That(File.ReadAllText(existing), Is.EqualTo("keep"));
            AssertCanOpenExclusively(existing);

            var missing = InTemporaryDirectory("not-created", "target.txt");
            Assert.Throws<ArgumentNullException>(() => WriteNull(kind, missing));
            Assert.That(Directory.Exists(Path.GetDirectoryName(missing)), Is.False);
        }

        [Test]
        public void EnumerationFailureNeitherTruncatesExistingFileNorCreatesParents()
        {
            var existing = InTemporaryDirectory("existing.txt");
            File.WriteAllText(existing, "keep");
            Assert.Throws<InvalidOperationException>(() => FileWrite.WriteLines(existing, FailingLines()));
            Assert.That(File.ReadAllText(existing), Is.EqualTo("keep"));
            AssertCanOpenExclusively(existing);

            var missing = InTemporaryDirectory("not-created", "target.txt");
            Assert.Throws<InvalidOperationException>(() => FileWrite.WriteLines(missing, FailingLines()));
            Assert.That(Directory.Exists(Path.GetDirectoryName(missing)), Is.False);
        }

        [Test]
        public void MissingReadsThrowInsteadOfReturningEmptyContent()
        {
            var missing = InTemporaryDirectory("missing.txt");
            Assert.Throws<FileNotFoundException>(() => FileRead.ReadText(missing));
            Assert.Throws<FileNotFoundException>(() => FileRead.ReadBytes(missing));
            Assert.Throws<FileNotFoundException>(() => FileRead.ReadLines(missing));
            Assert.That(File.Exists(missing), Is.False);
        }

        [Test]
        public void ReadWriteNullPathsUseStandardArgumentExceptions()
        {
            foreach (var action in ReadWriteOperations(null))
                Assert.Throws<ArgumentNullException>(() => action());
            Assert.That(Directory.GetFileSystemEntries(temporaryDirectory), Is.Empty);
        }

        [Test]
        public void LegitimateWhitespaceInsideFileNamesIsNotTrimmed()
        {
            var path = InTemporaryDirectory(" leading-name.txt");
            FileWrite.WriteText(path, "keep");
            Assert.That(File.ReadAllText(path), Is.EqualTo("keep"));
            Assert.That(File.Exists(InTemporaryDirectory("leading-name.txt")), Is.False);
        }

        [Test]
        public void ReadsReleaseFilesBeforeReturningMaterializedResults()
        {
            var path = InTemporaryDirectory("read.txt");
            File.WriteAllText(path, "one\ntwo");
            var text = FileRead.ReadText(path);
            AssertCanOpenExclusively(path);
            var bytes = FileRead.ReadBytes(path);
            AssertCanOpenExclusively(path);
            var lines = FileRead.ReadLines(path);
            AssertCanOpenExclusively(path);
            File.Delete(path);
            Assert.That(text, Is.EqualTo("one\ntwo"));
            Assert.That(bytes, Is.Not.Empty);
            Assert.That(lines, Is.EqualTo(new[] { "one", "two" }));
        }

        private IEnumerable<Action> ReadWriteOperations(string path)
        {
            yield return () => FileRead.ReadText(path);
            yield return () => FileRead.ReadBytes(path);
            yield return () => FileRead.ReadLines(path);
            yield return () => FileWrite.WriteText(path, "content");
            yield return () => FileWrite.WriteBytes(path, new byte[] { 1 });
            yield return () => FileWrite.WriteLines(path, new[] { "line" });
            yield return () => FileWrite.Create(path);
            yield return () => FileWrite.Clear(path);
        }

        private string InTemporaryDirectory(params string[] parts)
        {
            return Path.Combine(temporaryDirectory, Path.Combine(parts));
        }

        private static void AssertCanOpenExclusively(string path)
        {
            using (new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
            }
        }

        private static void WriteEmpty(string kind, string path, bool append)
        {
            switch (kind)
            {
                case "text": FileWrite.WriteText(path, string.Empty, append); break;
                case "bytes": FileWrite.WriteBytes(path, new byte[0], append); break;
                case "lines": FileWrite.WriteLines(path, new string[0], append); break;
                default: throw new ArgumentException("Unknown test kind.", nameof(kind));
            }
        }

        private static void WriteNull(string kind, string path)
        {
            switch (kind)
            {
                case "text": FileWrite.WriteText(path, null); break;
                case "bytes": FileWrite.WriteBytes(path, null); break;
                case "lines": FileWrite.WriteLines(path, null); break;
                default: throw new ArgumentException("Unknown test kind.", nameof(kind));
            }
        }

        private static IEnumerable<string> FailingLines()
        {
            yield return "would truncate";
            throw new InvalidOperationException("Enumeration failed.");
        }
    }
}
