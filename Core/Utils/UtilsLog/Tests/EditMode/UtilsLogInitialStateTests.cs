using System;
using NUnit.Framework;
using UnityEngine;

namespace GameSDK
{
    [TestFixture]
    public sealed class UtilsLogInitialStateTests
    {
        // 在新加载的脚本域中首先单独运行本例。普通规则测试的基线不能证明初始值。
        [Test, Explicit("新脚本域加载后，在任何 UtilsLog 配置调用及普通规则测试之前，单独运行本例。")]
        public void FreshDomainForwardsEveryLevelBeforeAnyConfiguration()
        {
            using (var capture = new LogCapture())
            {
                try
                {
                    UtilsLog.Log("initial info");
                    UtilsLog.Warning("initial warning");
                    UtilsLog.Error("initial error");
                    var exception = new InvalidOperationException("initial exception");
                    UtilsLog.Exception(exception);

                    Assert.That(capture.Entries.ConvertAll(entry => entry.Type),
                        Is.EqualTo(new[] { LogType.Log, LogType.Warning, LogType.Error, LogType.Exception }));
                    Assert.That(capture.Entries[3].Exception, Is.SameAs(exception));
                }
                finally
                {
                    UtilsLog.SetEnabled(true);
                    UtilsLog.SetLevel(LogLevel.Info);
                }
            }
        }
    }
}
