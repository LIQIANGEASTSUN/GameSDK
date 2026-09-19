using System;
using System.Collections.Generic;

namespace GameSDK
{
    /// <summary>
    /// 同步事件总线。调用方保证在 Unity 主线程使用，并负责成对注销与显式释放。
    /// 一个事件名在本实例内只绑定一种参数签名，释放后旧实例不能再次订阅或发布。
    /// </summary>
    public sealed class EventBus : Singleton<EventBus>
    {
        private readonly Dictionary<string, EventChannel> _channels = new (StringComparer.Ordinal);

        // 日志处理器可能释放当前实例并取得新实例，保护必须跨实例共享。
        private static bool _reportingException;
        private bool _active;

        private EventBus() { }

        protected override void OnInitialize()
        {
            _active = true;
        }

        protected override void OnRelease()
        {
            _active = false;
            foreach (EventChannel channel in _channels.Values)
                channel.Close();

            _channels.Clear();
        }

        #region 无参
        public void Subscribe(string key, Action callback)
        {
            SubscribeCore(key, callback);
        }

        public void Unsubscribe(string key, Action callback)
        {
            UnsubscribeCore(key, callback);
        }

        public void Publish(string key)
        {
            EventChannel channel = GetPublishChannel<Action>(key);
            if (channel == null)
                return;

            int count = channel.BeginPublish();
            try
            {
                for (int index = 0; index < count && _active; index++)
                {
                    Action callback = (Action)channel.CallbackAt(index);
                    if (callback == null)
                        continue;

                    try { callback(); }
                    catch (Exception exception) { ReportException(exception); }
                }
            }
            finally { channel.EndPublish(); }
        }
        #endregion 无参

        #region 1 参
        public void Subscribe<T1>(string key, Action<T1> callback)
        {
            SubscribeCore(key, callback);
        }

        public void Unsubscribe<T1>(string key, Action<T1> callback)
        {
            UnsubscribeCore(key, callback);
        }

        public void Publish<T1>(string key, T1 arg1)
        {
            EventChannel channel = GetPublishChannel<Action<T1>>(key);
            if (channel == null)
                return;

            int count = channel.BeginPublish();
            try
            {
                for (int index = 0; index < count && _active; index++)
                {
                    Action<T1> callback = (Action<T1>)channel.CallbackAt(index);
                    if (callback == null)
                        continue;

                    try { callback(arg1); }
                    catch (Exception exception) { ReportException(exception); }
                }
            }
            finally { channel.EndPublish(); }
        }
        #endregion  1 参

        #region 2 参
        public void Subscribe<T1, T2>(string key, Action<T1, T2> callback)
        {
            SubscribeCore(key, callback);
        }

        public void Unsubscribe<T1, T2>(string key, Action<T1, T2> callback)
        {
            UnsubscribeCore(key, callback);
        }

        public void Publish<T1, T2>(string key, T1 arg1, T2 arg2)
        {
            EventChannel channel = GetPublishChannel<Action<T1, T2>>(key);
            if (channel == null)
                return;

            int count = channel.BeginPublish();
            try
            {
                for (int index = 0; index < count && _active; index++)
                {
                    Action<T1, T2> callback = (Action<T1, T2>)channel.CallbackAt(index);
                    if (callback == null)
                        continue;

                    try { callback(arg1, arg2); }
                    catch (Exception exception) { ReportException(exception); }
                }
            }
            finally { channel.EndPublish(); }
        }
        #endregion  2 参

        #region 3 参
        public void Subscribe<T1, T2, T3>(string key, Action<T1, T2, T3> callback)
        {
            SubscribeCore(key, callback);
        }

        public void Unsubscribe<T1, T2, T3>(string key, Action<T1, T2, T3> callback)
        {
            UnsubscribeCore(key, callback);
        }

        public void Publish<T1, T2, T3>(string key, T1 arg1, T2 arg2, T3 arg3)
        {
            EventChannel channel = GetPublishChannel<Action<T1, T2, T3>>(key);
            if (channel == null)
                return;

            int count = channel.BeginPublish();
            try
            {
                for (int index = 0; index < count && _active; index++)
                {
                    Action<T1, T2, T3> callback = (Action<T1, T2, T3>)channel.CallbackAt(index);
                    if (callback == null)
                        continue;

                    try { callback(arg1, arg2, arg3); }
                    catch (Exception exception) { ReportException(exception); }
                }
            }
            finally { channel.EndPublish(); }
        }
        #endregion  3 参

        private void SubscribeCore<TCallback>(string key, TCallback callback) where TCallback : Delegate
        {
            ValidateKey(key);
            ValidateCallback(callback);
            EnsureActive();

            EventChannel channel = FindChannel<TCallback>(key);
            if (channel != null)
            {
                channel.Subscribe(callback);
                return;
            }

            // 首次成功订阅才绑定签名；所有输入检查均在修改容器之前。
            channel = new EventChannel(typeof(TCallback));
            channel.Subscribe(callback);
            _channels.Add(key, channel);
        }

        private void UnsubscribeCore<TCallback>(string key, TCallback callback) where TCallback : Delegate
        {
            ValidateKey(key);
            ValidateCallback(callback);
            if (!_active)
                return;

            FindChannel<TCallback>(key)?.Unsubscribe(callback);
        }

        private EventChannel GetPublishChannel<TCallback>(string key) where TCallback : Delegate
        {
            ValidateKey(key);
            EnsureActive();
            return FindChannel<TCallback>(key);
        }

        private EventChannel FindChannel<TCallback>(string key) where TCallback : Delegate
        {
            if (!_channels.TryGetValue(key, out EventChannel channel))
                return null;

            // 签名取调用时的 Action 类型，保留参数数量、顺序及 Type 身份。
            if (channel.Signature == typeof(TCallback))
                return channel;

            throw new InvalidOperationException("The event key is already bound to a different parameter signature: " + key);
        }

        private void EnsureActive()
        {
            if (!_active)
                throw new InvalidOperationException("This EventBus instance has been released.");
        }

        private static void ValidateKey(string key)
        {
            if (key == null)
                throw new ArgumentNullException(nameof(key));
            if (key.Length == 0)
                throw new ArgumentException("The event key must not be empty.", nameof(key));
        }

        private static void ValidateCallback(Delegate callback)
        {
            if (callback == null)
                throw new ArgumentNullException(nameof(callback));
            if (callback.GetInvocationList().Length != 1)
                throw new ArgumentException("The callback must contain exactly one invocation.", nameof(callback));
        }

        private static void ReportException(Exception exception)
        {
            if (_reportingException)
                return;

            _reportingException = true;
            try
            {
                UtilsLog.Exception(exception);
            }
            catch (Exception)
            {
                // 日志入口失败时结束本次报告，不能递归报告或中断后续有效监听。
            }
            finally { _reportingException = false; }
        }
    }
}
