using GameInterface;

namespace GameSDK
{
    /// <summary>在 SDK 内部选择平台实现，向消费者提供设备契约。</summary>
    public static class DeviceService
    {
        /// <summary>
        /// 在 Unity 主线程创建独立服务实例，不读取标识或申请权限。
        /// </summary>
        public static IDevice Create()
        {
#if UNITY_EDITOR
            return new DeviceFallback();
#elif UNITY_ANDROID
            return new DeviceAndroid();
#elif UNITY_IOS
            return new DeviceIOS();
#else
            return new DeviceFallback();
#endif
        }
    }
}
