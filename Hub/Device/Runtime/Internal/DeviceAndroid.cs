#if UNITY_ANDROID && !UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Threading;
using GameInterface;
using UnityEngine;

namespace GameSDK
{
    internal sealed class DeviceAndroid : IDevice
    {
        // DeviceService.Create must run on the Unity main thread.
        private readonly int creationThreadId = Thread.CurrentThread.ManagedThreadId;

        public string GetDeviceId(Dictionary<string, object> parameters = null)
        {
            string deviceId = SystemInfo.deviceUniqueIdentifier;
            if (string.IsNullOrEmpty(deviceId) || deviceId == SystemInfo.unsupportedIdentifier)
                throw new InvalidOperationException("Android device ID is unavailable.");
            return deviceId;
        }

        public string GetAdvertisingId()
        {
            return ReadAdvertisingId();
        }

        public bool GetAdvertisingTrackingEnabled()
        {
            return ReadAdvertisingId().Length != 0;
        }

        public void DeleteIosDeviceId(Dictionary<string, object> parameters = null)
        {
        }

        private string ReadAdvertisingId()
        {
            if (Thread.CurrentThread.ManagedThreadId == creationThreadId)
                throw new InvalidOperationException("Android advertising queries must run on a worker thread.");

            try
            {
                string result = null;
                AndroidJNI.InvokeAttached(() =>
                {
                    result = ReadAdvertisingIdCore();
                });
                return result;
            }
            catch (AndroidJavaException exception)
            {
                throw new InvalidOperationException("Android device operation failed: read advertising information.", exception);
            }
        }

        private static string ReadAdvertisingIdCore()
        {
            RequireNonUiThread();
            using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
            {
                if (activity == null)
                    throw new InvalidOperationException("Android activity is unavailable.");

                using (var context = activity.Call<AndroidJavaObject>("getApplicationContext"))
                {
                    if (context == null)
                        throw new InvalidOperationException("Android application context is unavailable.");

                    using (var client = new AndroidJavaClass("com.google.android.gms.ads.identifier.AdvertisingIdClient"))
                    using (var info = client.CallStatic<AndroidJavaObject>("getAdvertisingIdInfo", context))
                    {
                        if (info == null)
                            throw new InvalidOperationException("Android advertising information is unavailable.");
                        if (info.Call<bool>("isLimitAdTrackingEnabled"))
                            return string.Empty;

                        string id = info.Call<string>("getId");
                        if (string.IsNullOrEmpty(id))
                            return string.Empty;
                        foreach (char character in id)
                        {
                            if (character != '0' && character != '-')
                                return id;
                        }
                        return string.Empty;
                    }
                }
            }
        }

        private static void RequireNonUiThread()
        {
            using (var looper = new AndroidJavaClass("android.os.Looper"))
            using (var mainLooper = looper.CallStatic<AndroidJavaObject>("getMainLooper"))
            using (var currentLooper = looper.CallStatic<AndroidJavaObject>("myLooper"))
            {
                if (mainLooper == null)
                    throw new InvalidOperationException("Android main looper is unavailable.");
                if (currentLooper != null && AndroidJNI.IsSameObject(currentLooper.GetRawObject(), mainLooper.GetRawObject()))
                    throw new InvalidOperationException("Advertising queries must not run on the Android UI thread.");
            }
        }
    }
}
#endif
