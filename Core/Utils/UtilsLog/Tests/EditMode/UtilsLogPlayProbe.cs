using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace GameSDK
{
    // 手动验证消费者：仅在菜单启用后运行，不改场景或 Enter Play Mode 设置。
    [InitializeOnLoad]
    internal static class UtilsLogPlayProbe
    {
        private const string MenuRoot = "Tools/GameSDK/Log/";
        private const string ArmedKey = "GameSDK.Log.PlayProbe.Armed";
        private const string ReloadCountKey = "GameSDK.Log.PlayProbe.ReloadCount";
        private const string NoReloadCountKey = "GameSDK.Log.PlayProbe.NoReloadCount";
        private static GameObject exceptionContext;

        static UtilsLogPlayProbe()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            AssemblyReloadEvents.beforeAssemblyReload -= DestroyContext;
            AssemblyReloadEvents.beforeAssemblyReload += DestroyContext;
        }

        [MenuItem(MenuRoot + "Arm Play Session Checks")]
        private static void Arm()
        {
            SessionState.SetBool(ArmedKey, true);
            SessionState.SetInt(ReloadCountKey, 0);
            SessionState.SetInt(NoReloadCountKey, 0);
            UtilsLog.SetEnabled(false);
            UtilsLog.SetLevel(LogLevel.Error);
            Debug.Log("UtilsLog 验证已启用，已显式设为关闭 / Error：使用无其他 UtilsLog 配置调用的隔离空场景；" +
                "先设置 Domain Reload 再 Arm，每种设置连续进入 Play 两次，切换设置后重新 Arm。" +
                "开启时应重新初始化，关闭时应保留配置；组内不要重新 Arm、Disarm、运行规则测试或重编译。" +
                "每次须出现 PASS，失败不能视作通过。" +
                "检查结束后执行 Disarm Play Session Checks，并恢复原场景和 Play 设置。");
        }

        [MenuItem(MenuRoot + "Arm Play Session Checks", true)]
        private static bool CanArm()
        {
            return !EditorApplication.isPlayingOrWillChangePlaymode;
        }

        [MenuItem(MenuRoot + "Disarm Play Session Checks")]
        private static void Disarm()
        {
            SessionState.SetBool(ArmedKey, false);
            UtilsLog.SetEnabled(true);
            UtilsLog.SetLevel(LogLevel.Info);
            DestroyContext();
            Debug.Log("UtilsLog 验证已停用，模块配置恢复为开启 / Info。");
        }

        [MenuItem(MenuRoot + "Disarm Play Session Checks", true)]
        private static bool CanDisarm()
        {
            return !EditorApplication.isPlayingOrWillChangePlaymode && SessionState.GetBool(ArmedKey, false);
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingPlayMode)
                DestroyContext();

            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(ArmedKey, false))
                return;

            bool reloadDisabled = EditorSettings.enterPlayModeOptionsEnabled &&
                (EditorSettings.enterPlayModeOptions & EnterPlayModeOptions.DisableDomainReload) != 0;
            string mode = reloadDisabled ? "关闭" : "开启";
            try
            {
                // EnteredPlayMode 在脚本域与场景启动之后；
                // 必须用隔离空场景，避免宿主在本观察点之前应用自己的配置。
                using (var capture = new LogCapture())
                {
                    try
                    {
                        EmitEveryLevel();
                        if (reloadDisabled)
                        {
                            Assert.That(capture.CallCount, Is.Zero, "关闭 Domain Reload 时应保留关闭状态。");
                            UtilsLog.SetEnabled(true);
                            EmitEveryLevel();
                            AssertTypes(capture, LogType.Error, LogType.Exception);
                        }
                        else
                        {
                            AssertTypes(capture, LogType.Log, LogType.Warning, LogType.Error, LogType.Exception);
                        }

                        capture.Entries.Clear();
                        UtilsLog.SetEnabled(true);
                        UtilsLog.SetLevel(LogLevel.Warning);
                        EmitEveryLevel();
                        AssertTypes(capture, LogType.Warning, LogType.Error, LogType.Exception);

                        int count = capture.CallCount;
                        UtilsLog.SetEnabled(false);
                        UtilsLog.SetLevel(LogLevel.Error);
                        EmitEveryLevel();
                        Assert.That(capture.CallCount, Is.EqualTo(count));
                    }
                    finally
                    {
                        // 保留非默认配置，供下一次 Play 区分字段重建与原配置保留。
                        UtilsLog.SetEnabled(false);
                        UtilsLog.SetLevel(LogLevel.Error);
                    }
                }

                string key = reloadDisabled ? NoReloadCountKey : ReloadCountKey;
                int countForMode = SessionState.GetInt(key, 0) + 1;
                SessionState.SetInt(key, countForMode);
                string stateResult = reloadDisabled ? "关闭状态与 Error 等级保留" : "默认字段初始化";
                Debug.Log($"UtilsLog Play PASS：Domain Reload {mode}，本轮第 {countForMode} 次。" +
                    $"{stateResult}及随后显式配置已验证，现保留关闭 / Error；退出后再次 Play。");
            }
            catch (Exception exception)
            {
                Debug.LogError($"UtilsLog Play FAIL：Domain Reload {mode}。本次不计入通过次数。\n{exception}");
            }
        }

        [MenuItem(MenuRoot + "Emit Exception With Context")]
        private static void EmitExceptionWithContext()
        {
            ILogger logger = Debug.unityLogger;
            bool previousEnabled = logger.logEnabled;
            LogType previousFilter = logger.filterLogType;
            ILogHandler previousHandler = logger.logHandler;
            try
            {
                if (exceptionContext == null)
                    exceptionContext = new GameObject("UtilsLog exception context") { hideFlags = HideFlags.DontSave };

                logger.logEnabled = true;
                logger.filterLogType = LogType.Log;
                UtilsLog.SetEnabled(true);
                UtilsLog.SetLevel(LogLevel.Info);
                try
                {
                    ThrowOriginalProbeException();
                }
                catch (InvalidOperationException exception)
                {
                    UtilsLog.Exception(exception, exceptionContext);
                }
            }
            finally
            {
                // 与会话检查相同，留非默认值供下一次进入 Play 验证。
                UtilsLog.SetEnabled(false);
                UtilsLog.SetLevel(LogLevel.Error);
                logger.logHandler = previousHandler;
                logger.logEnabled = previousEnabled;
                logger.filterLogType = previousFilter;
            }
        }

        [MenuItem(MenuRoot + "Emit Exception With Context", true)]
        private static bool CanEmitException()
        {
            return EditorApplication.isPlaying && SessionState.GetBool(ArmedKey, false);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ThrowOriginalProbeException()
        {
            throw new InvalidOperationException("UtilsLog original exception / 原始异常定位验证");
        }

        private static void EmitEveryLevel()
        {
            UtilsLog.Log("play info");
            UtilsLog.Warning("play warning");
            UtilsLog.Error("play error");
            UtilsLog.Exception(new InvalidOperationException("play exception"));
        }

        private static void AssertTypes(LogCapture capture, params LogType[] expected)
        {
            Assert.That(capture.Entries.ConvertAll(entry => entry.Type), Is.EqualTo(expected));
        }

        private static void DestroyContext()
        {
            if (exceptionContext != null)
                UnityEngine.Object.DestroyImmediate(exceptionContext);
            exceptionContext = null;
        }
    }
}
