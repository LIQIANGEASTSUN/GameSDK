#if UNITY_IOS && !UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using GameInterface;

namespace GameSDK
{
    internal sealed class DeviceIOS : IDevice
    {
        public string GetDeviceId(Dictionary<string, object> parameters = null)
        {
            IOSKeychainOptions options = IOSKeychainOptions.FromParameters(parameters);
            try
            {
                IntPtr value = IntPtr.Zero;
                try
                {
                    CheckStatus(GameSDKDevice_GetDeviceId(options.Account, options.Service,
                        options.Identifier, options.Description, out value), "read/create Keychain device ID");
                    return CopyString(value);
                }
                finally
                {
                    if (value != IntPtr.Zero)
                        GameSDKDevice_FreeString(value);
                }
            }
            catch (Exception exception) when (exception is DllNotFoundException ||
                exception is EntryPointNotFoundException || exception is BadImageFormatException)
            {
                throw new InvalidOperationException("iOS device bridge is unavailable.", exception);
            }
        }

        public string GetAdvertisingId()
        {
            return ReadAdvertisingInfo().Id;
        }

        public bool GetAdvertisingTrackingEnabled()
        {
            return ReadAdvertisingInfo().Enabled;
        }

        public void DeleteIosDeviceId(Dictionary<string, object> parameters = null)
        {
            IOSKeychainOptions options = IOSKeychainOptions.FromParameters(parameters);
            try
            {
                CheckStatus(GameSDKDevice_DeleteDeviceId(options.Account, options.Service,
                    options.Identifier), "delete Keychain device ID");
            }
            catch (Exception exception) when (exception is DllNotFoundException ||
                exception is EntryPointNotFoundException || exception is BadImageFormatException)
            {
                throw new InvalidOperationException("iOS device bridge is unavailable.", exception);
            }
        }

        private static (string Id, bool Enabled) ReadAdvertisingInfo()
        {
            try
            {
                IntPtr value = IntPtr.Zero;
                try
                {
                    CheckStatus(GameSDKDevice_GetAdvertisingInfo(out value, out int enabled), "read advertising information");
                    return (CopyString(value), enabled != 0);
                }
                finally
                {
                    if (value != IntPtr.Zero)
                        GameSDKDevice_FreeString(value);
                }
            }
            catch (Exception exception) when (exception is DllNotFoundException ||
                exception is EntryPointNotFoundException || exception is BadImageFormatException)
            {
                throw new InvalidOperationException("iOS device bridge is unavailable.", exception);
            }
        }

        private static string CopyString(IntPtr value)
        {
            if (value == IntPtr.Zero)
                throw new InvalidOperationException("iOS device bridge returned no string buffer.");
            return Marshal.PtrToStringUTF8(value);
        }

        private static void CheckStatus(int status, string operation)
        {
            if (status != 0)
                throw new InvalidOperationException("iOS device operation failed: " + operation + "; OSStatus=" + status + ".");
        }

        [DllImport("__Internal")]
        private static extern int GameSDKDevice_GetDeviceId(
            [MarshalAs(UnmanagedType.LPUTF8Str)] string account,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string service,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string identifier,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string description, out IntPtr value);

        [DllImport("__Internal")]
        private static extern int GameSDKDevice_DeleteDeviceId(
            [MarshalAs(UnmanagedType.LPUTF8Str)] string account,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string service,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string identifier);

        [DllImport("__Internal")]
        private static extern int GameSDKDevice_GetAdvertisingInfo(out IntPtr value, out int enabled);

        [DllImport("__Internal")]
        private static extern void GameSDKDevice_FreeString(IntPtr value);
    }
}
#endif
