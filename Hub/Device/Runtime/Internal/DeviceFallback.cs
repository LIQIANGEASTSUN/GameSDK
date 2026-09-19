using System.Collections.Generic;
using GameInterface;
using UnityEngine;

namespace GameSDK
{
    internal sealed class DeviceFallback : IDevice
    {
        public string GetDeviceId(Dictionary<string, object> parameters = null)
        {
            return SystemInfo.deviceUniqueIdentifier ?? string.Empty;
        }

        public string GetAdvertisingId()
        {
            return string.Empty;
        }

        public bool GetAdvertisingTrackingEnabled()
        {
            return false;
        }

        public void DeleteIosDeviceId(Dictionary<string, object> parameters = null)
        {
        }
    }
}
