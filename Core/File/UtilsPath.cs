using System;
using System.IO;
using UnityEngine;

namespace GameSDK
{
    /// <summary>提供路径组合和 Unity 常用根路径，不负责文件访问或 URI 转换。</summary>
    public static class UtilsPath
    {
        /// <summary>按当前平台规则组合路径，再将反斜杠统一为正斜杠。</summary>
        /// <param name="paths">待组合的路径段；零个元素返回空串，空段按 Path.Combine 忽略。</param>
        /// <returns>统一为正斜杠的路径；保留已有 URI 前缀，不编码或限制在首段目录内。</returns>
        /// <exception cref="ArgumentNullException">数组或任意元素为 null。</exception>
        /// <exception cref="ArgumentException">路径含当前平台标准库不接受的字符。</exception>
        public static string CombinePath(params string[] paths)
        {
            return Path.Combine(paths).Replace('\\', '/');
        }

        /// <summary>直接取得 Unity 当前的 StreamingAssets 路径，不修改前缀或分隔符。</summary>
        public static string StreamingAssetsPath => Application.streamingAssetsPath;

        /// <summary>在 StreamingAssets 根后组合相对子路径，再统一为正斜杠；不执行文件访问。</summary>
        /// <param name="paths">调用方提供的相对子路径段；不额外检查根路径或越界。</param>
        /// <returns>组合后的路径；零参数返回统一分隔符的根，保留 Unity 的 URI 前缀。</returns>
        /// <exception cref="ArgumentNullException">数组或任意元素为 null。</exception>
        /// <exception cref="ArgumentException">路径含当前平台标准库不接受的字符。</exception>
        public static string GetStreamingAssetsFilePath(params string[] paths)
        {
            return CombinePath(StreamingAssetsPath, Path.Combine(paths));
        }

        /// <summary>直接取得 Unity 当前的持久化数据路径，不修改前缀或分隔符。</summary>
        public static string PersistentDataPath => Application.persistentDataPath;

        /// <summary>在持久化数据根后组合相对子路径，再统一为正斜杠。</summary>
        /// <param name="paths">调用方提供的相对子路径段；不额外检查根路径或越界。</param>
        /// <returns>组合后的路径；零参数返回统一分隔符的根路径。</returns>
        /// <exception cref="ArgumentNullException">数组或任意元素为 null。</exception>
        /// <exception cref="ArgumentException">路径含当前平台标准库不接受的字符。</exception>
        public static string GetPersistentDataPath(params string[] paths)
        {
            return CombinePath(PersistentDataPath, Path.Combine(paths));
        }
    }
}
