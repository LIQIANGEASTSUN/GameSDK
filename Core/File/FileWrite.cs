using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace GameSDK
{
    /// <summary>
    /// 同步写入本地文件，单次调用内释放全部资源；数据 null 检查在文件副作用前执行。
    /// I/O 失败原样抛出，不保证写入原子性或并发写入安全。
    /// </summary>
    public static class FileWrite
    {
        private static readonly UTF8Encoding TextEncoding = new (false);

        /// <summary>以 UTF-8 无 BOM 覆盖或追加文本，自动建立父目录；追加要求已有文件为 UTF-8。</summary>
        /// <param name="path">按 System.IO 本地语义访问的文件路径，不转换 URI。</param>
        /// <param name="content">待写文本，允许空串，不允许 null。</param>
        /// <param name="append">是否追加；默认覆盖，文件缺失时两种模式都会创建。</param>
        /// <exception cref="ArgumentNullException">路径或内容为 null。</exception>
        /// <exception cref="ArgumentException">标准库判断路径无效。</exception>
        /// <exception cref="IOException">文件写入失败。</exception>
        public static void WriteText(string path, string content, bool append = false)
        {
            if (content == null)
                throw new ArgumentNullException(nameof(content));

            EnsureParentDirectory(path);
            using (var writer = new StreamWriter(path, append, TextEncoding))
            {
                writer.Write(content);
            }
        }

        /// <summary>覆盖或追加完整字节数组，自动建立父目录。</summary>
        /// <param name="path">按 System.IO 本地语义访问的文件路径，不转换 URI。</param>
        /// <param name="data">待写字节，允许空数组，不允许 null。</param>
        /// <param name="append">是否追加；默认覆盖，文件缺失时两种模式都会创建。</param>
        /// <exception cref="ArgumentNullException">路径或数据为 null。</exception>
        /// <exception cref="ArgumentException">标准库判断路径无效。</exception>
        /// <exception cref="IOException">文件写入失败。</exception>
        public static void WriteBytes(string path, byte[] data, bool append = false)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));

            EnsureParentDirectory(path);
            FileMode mode = append ? FileMode.Append : FileMode.Create;
            using (var stream = new FileStream(path, mode, FileAccess.Write, FileShare.Read))
            {
                stream.Write(data, 0, data.Length);
            }
        }

        /// <summary>
        /// 完整物化输入后，以 UTF-8 无 BOM 一次打开流写入全部行，自动建立父目录。
        /// 每行含平台换行符；追加要求已有文件为 UTF-8，不补齐原文件末尾缺失的换行。
        /// </summary>
        /// <param name="path">按 System.IO 本地语义访问的文件路径，不转换 URI。</param>
        /// <param name="lines">按顺序写入的行，集合不允许 null，null 元素按空行写入。</param>
        /// <param name="append">是否追加；默认覆盖，空集合在覆盖模式下创建或清空文件。</param>
        /// <exception cref="ArgumentNullException">路径或行集合为 null。</exception>
        /// <exception cref="ArgumentException">标准库判断路径无效。</exception>
        /// <exception cref="IOException">文件写入失败；枚举输入的异常也会原样抛出。</exception>
        public static void WriteLines(string path, IEnumerable<string> lines, bool append = false)
        {
            if (lines == null)
                throw new ArgumentNullException(nameof(lines));

            var materializedLines = new List<string>(lines);
            EnsureParentDirectory(path);
            using (var writer = new StreamWriter(path, append, TextEncoding))
            {
                foreach (var line in materializedLines)
                    writer.WriteLine(line);
            }
        }

        /// <summary>创建空文件并建立缺失父目录；默认保护已有文件，显式覆盖时清空。</summary>
        /// <param name="path">按 System.IO 本地语义访问的文件路径，不转换 URI。</param>
        /// <param name="overwrite">是否允许清空已有文件，默认不允许。</param>
        /// <exception cref="ArgumentNullException">路径为 null。</exception>
        /// <exception cref="ArgumentException">标准库判断路径无效。</exception>
        /// <exception cref="IOException">默认模式下文件已存在，或创建失败。</exception>
        public static void Create(string path, bool overwrite = false)
        {
            EnsureParentDirectory(path);
            FileMode mode = overwrite ? FileMode.Create : FileMode.CreateNew;
            using (new FileStream(path, mode, FileAccess.Write, FileShare.Read))
            {
            }
        }

        /// <summary>截断已有文件为零字节并保留文件；不创建文件或目录。</summary>
        /// <param name="path">按 System.IO 本地语义访问的文件路径，不转换 URI。</param>
        /// <exception cref="ArgumentNullException">路径为 null。</exception>
        /// <exception cref="ArgumentException">标准库判断路径无效。</exception>
        /// <exception cref="IOException">文件或父目录缺失，或清空失败。</exception>
        public static void Clear(string path)
        {
            using (new FileStream(path, FileMode.Truncate, FileAccess.Write, FileShare.Read))
            {
            }
        }

        private static void EnsureParentDirectory(string path)
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);
        }
    }
}
