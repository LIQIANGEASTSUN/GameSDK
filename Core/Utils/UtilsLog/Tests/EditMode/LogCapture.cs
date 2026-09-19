using System;
using System.Collections.Generic;
using UnityEngine;

namespace GameSDK
{
    // 仅在串行隔离的验证期间替换处理器，避免依赖 Console 的可见性。
    internal sealed class LogCapture : ILogHandler, IDisposable
    {
        internal sealed class Entry
        {
            internal LogType Type;
            internal UnityEngine.Object Context;
            internal string Format;
            internal object[] Arguments;
            internal Exception Exception;
        }

        private readonly ILogger logger;
        private readonly ILogHandler previousHandler;
        private readonly bool previousEnabled;
        private readonly LogType previousFilter;

        internal readonly List<Entry> Entries = new List<Entry>();
        internal Exception Failure;
        internal int CallCount;

        internal LogCapture()
        {
            logger = Debug.unityLogger;
            previousHandler = logger.logHandler;
            previousEnabled = logger.logEnabled;
            previousFilter = logger.filterLogType;
            logger.logHandler = this;
            logger.logEnabled = true;
            logger.filterLogType = LogType.Log;
        }

        public void LogFormat(LogType logType, UnityEngine.Object context, string format, params object[] args)
        {
            Record(new Entry { Type = logType, Context = context, Format = format, Arguments = args });
        }

        public void LogException(Exception exception, UnityEngine.Object context)
        {
            Record(new Entry { Type = LogType.Exception, Context = context, Exception = exception });
        }

        private void Record(Entry entry)
        {
            CallCount++;
            if (Failure != null)
                throw Failure;

            Entries.Add(entry);
        }

        public void Dispose()
        {
            logger.logHandler = previousHandler;
            logger.logEnabled = previousEnabled;
            logger.filterLogType = previousFilter;
        }
    }
}
