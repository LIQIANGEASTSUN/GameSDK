using System;
using System.Collections.Generic;

namespace GameSDK
{
    internal sealed class EventChannel
    {
        private const int CompactThreshold = 32;
        private readonly List<Delegate> _callbacks = new ();
        // Unity Mono 的 Delegate.Equals 可能忽略逆变委托的实际类型，键中显式保留该身份。
        private readonly Dictionary<(Type, Delegate), int> _indices = new ();
        private int _publishDepth;

        internal Type Signature { get; }

        internal EventChannel(Type signature)
        {
            Signature = signature;
        }

        internal void Subscribe(Delegate callback)
        {
            var identity = (callback.GetType(), callback);
            if (!_indices.TryAdd(identity, _callbacks.Count))
                throw new InvalidOperationException("The callback is already subscribed to this event.");

            _callbacks.Add(callback);
        }

        internal void Unsubscribe(Delegate callback)
        {
            var identity = (callback.GetType(), callback);
            if (!_indices.Remove(identity, out int index))
                return;

            _callbacks[index] = null;
            CompactIfNeeded();
        }

        internal int BeginPublish()
        {
            _publishDepth++;
            return _callbacks.Count;
        }

        internal Delegate CallbackAt(int index)
        {
            return _callbacks[index];
        }

        internal void EndPublish()
        {
            _publishDepth--;
            CompactIfNeeded();
        }

        internal void Close()
        {
            _indices.Clear();
            // 活跃发布必须保留 Count/索引，同时立即释放所有尚存的回调引用。
            for (int index = 0; index < _callbacks.Count; index++)
                _callbacks[index] = null;

            CompactIfNeeded();
        }

        private void CompactIfNeeded()
        {
            if (_publishDepth != 0)
                return;

            if (_indices.Count == 0)
            {
                _callbacks.Clear();
                return;
            }

            int removedCount = _callbacks.Count - _indices.Count;
            if (removedCount < CompactThreshold || removedCount < _indices.Count)
                return;

            int destination = 0;
            for (int source = 0; source < _callbacks.Count; source++)
            {
                Delegate callback = _callbacks[source];
                if (callback == null)
                    continue;

                _callbacks[destination] = callback;
                _indices[(callback.GetType(), callback)] = destination;
                destination++;
            }

            _callbacks.RemoveRange(destination, _callbacks.Count - destination);
        }
    }
}
