using System;
using UnityEngine;

namespace GameSDK
{
    /// <summary>
    /// 日志的最低输出等级，数值越大表示级别越高。
    /// </summary>
    public enum LogLevel
    {
        Info = 0,
        Warning = 1,
        Error = 2
    }
    
    /// <summary>
    /// 使用共享开关和最低等级过滤本模块日志，再原样交给 Unity。
    /// </summary>
    public static class UtilsLog
    {
        private static bool _enable = true;
        private static LogLevel _level = LogLevel.Info;

        public static void SetEnabled(bool enabled)
        {
            _enable = enabled;
        }

        public static void SetLevel(LogLevel level)
        {
            _level = level;
        }

        public static void Log(string message)
        {
            if (ShouldLog(LogLevel.Info))
                Debug.Log(message);
        }

        public static void Warning(string message)
        {
            if (ShouldLog(LogLevel.Warning))
                Debug.LogWarning(message);
        }

        public static void Error(string message)
        {
            if (ShouldLog(LogLevel.Error))
                Debug.LogError(message);
        }

        public static void Exception(Exception exception, UnityEngine.Object context = null)
        {
            if (exception == null)
                throw new ArgumentNullException(nameof(exception));

            if (ShouldLog(LogLevel.Error))
                Debug.LogException(exception, context);
        }

        private static bool ShouldLog(LogLevel level)
        {
            return _enable && level >= _level;
        }
    }
}
