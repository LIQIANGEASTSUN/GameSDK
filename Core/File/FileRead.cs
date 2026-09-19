using System;
using System.IO;
using System.Text;

namespace GameSDK
{
    /// <summary>同步完整读取本地文件，方法返回前释放资源；缺失与 I/O 失败原样抛出。</summary>
    public static class FileRead
    {
        private static readonly UTF8Encoding TextEncoding = new (false);

        /// <summary>以 UTF-8 为默认编码完整读取文本并检测 Unicode BOM。</summary>
        /// <param name="path">按 System.IO 本地语义访问的文件路径，不转换 URI。</param>
        /// <returns>不包含 BOM 的文本；空文件返回空串。</returns>
        /// <exception cref="ArgumentNullException">路径为 null。</exception>
        /// <exception cref="ArgumentException">标准库判断路径无效。</exception>
        /// <exception cref="IOException">文件缺失或读取失败。</exception>
        public static string ReadText(string path)
        {
            return File.ReadAllText(path, TextEncoding);
        }

        /// <summary>完整读取本地文件原始字节，不进行文本解码。</summary>
        /// <param name="path">按 System.IO 本地语义访问的文件路径，不转换 URI。</param>
        /// <returns>已物化的全部字节；空文件返回空数组。</returns>
        /// <exception cref="ArgumentNullException">路径为 null。</exception>
        /// <exception cref="ArgumentException">标准库判断路径无效。</exception>
        /// <exception cref="IOException">文件缺失或读取失败。</exception>
        public static byte[] ReadBytes(string path)
        {
            return File.ReadAllBytes(path);
        }

        /// <summary>以 UTF-8 为默认编码检测 BOM 并完整读取全部行，不保留行终止符。</summary>
        /// <param name="path">按 System.IO 本地语义访问的文件路径，不转换 URI。</param>
        /// <returns>已物化的各行文本；空文件返回空数组。</returns>
        /// <exception cref="ArgumentNullException">路径为 null。</exception>
        /// <exception cref="ArgumentException">标准库判断路径无效。</exception>
        /// <exception cref="IOException">文件缺失或读取失败，不返回部分结果。</exception>
        public static string[] ReadLines(string path)
        {
            return File.ReadAllLines(path, TextEncoding);
        }
    }
}
