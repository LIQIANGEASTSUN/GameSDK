using UnityEngine;

namespace GameSDK
{
    public class UtilsApplication
    {
        /// <summary>
        /// 设置目标帧率，不保证实际帧率达到该值。
        /// 桌面和 Web 平台在 QualitySettings.vSyncCount 不为 0 时忽略该设置；
        /// XR 平台的帧率由 XR SDK 控制。
        /// </summary>
        /// <param name="frame">目标帧率（每秒帧数）；-1 使用 Unity 的平台默认行为。</param>
        public static void SetTargetFramesRate(int frame)
        {
            Application.targetFrameRate = frame;
        }

        /// <summary>
        /// 将后台加载线程的调度优先级设为本工具固定的 BelowNormal 档。
        /// 相关异步加载每帧在主线程的整合时间预算合计上限为 4ms，
        /// 此预算不是后台加载线程的运行时长。
        /// Default 指本工具的固定档位，不代表恢复 Unity 各平台的默认值。
        /// 该设置仅在构建后的 Player 生效，在 Editor 中无效。
        /// </summary>
        public static void SetBackgroundLoadingPriorityDefault()
        {
            Application.backgroundLoadingPriority = ThreadPriority.BelowNormal;
        }

        /// <summary>
        /// 将后台加载线程的调度优先级设为 Normal。
        /// 相关异步加载每帧在主线程的整合时间预算合计上限为 10ms。
        /// 该设置仅在构建后的 Player 生效，在 Editor 中无效。
        /// </summary>
        public static void SetBackgroundLoadingPriorityNormal()
        {
            Application.backgroundLoadingPriority = ThreadPriority.Normal;
        }

        /// <summary>
        /// 将后台加载线程的调度优先级设为 High。
        /// 相关异步加载每帧在主线程的整合时间预算合计上限为 50ms，
        /// 可能造成卡顿，仅适合能容忍较长帧耗时的加载阶段。
        /// 该设置仅在构建后的 Player 生效，在 Editor 中无效。
        /// </summary>
        public static void SetBackgroundLoadingPriorityHigh()
        {
            Application.backgroundLoadingPriority = ThreadPriority.High;
        }

        /// <summary>
        /// 请求退出 Player；在 Editor 中不执行操作。
        /// </summary>
        public static void Quit()
        {
#if !UNITY_EDITOR
            Application.Quit();
#endif
        }
    }
}