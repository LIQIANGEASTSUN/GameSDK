using System;
using System.Reflection;

namespace GameSDK
{
    /// <summary>
    /// 按需初始化的普通类单例。具体类型须以自身为 T，提供 private 无参构造且不提供 public 构造。
    /// 使用者负责停止消费者后显式释放；业务操作和旧引用的并发使用不由本类同步。
    /// 构造和钩子须同步完成，不得重入同类型获取或释放，也不得等待依赖本锁的线程。
    /// </summary>
    public abstract class Singleton<T> where T : Singleton<T>
    {
        private static readonly object _stateLock = new ();
        private static T _instance;

        protected Singleton()
        {
            if (GetType() != typeof(T))
            {
                throw new InvalidOperationException("A singleton's actual type must be its generic argument.");
            }
        }

        /// <summary>返回已初始化实例；并发调用等待当前创建或释放完成。</summary>
        public static T Instance
        {
            get
            {
                lock (_stateLock)
                {
                    if (null != _instance)
                    {
                        return _instance;
                    }

                    try
                    {
                        _instance = Construct();
                    }
                    catch
                    {
                        _instance = null;
                        throw;
                    }

                    return _instance;
                }
            }
        }

        /// <summary>等待当前操作完成后释放实例；没有实例时无操作，不主动创建。</summary>
        public static void ReleaseInstance()
        {
            lock (_stateLock)
            {
                if (null == _instance)
                {
                    return;
                }

                try
                {
                    _instance.OnRelease();
                }
                finally
                {
                    _instance = null;
                }
            }
        }

        /// <summary>取得实例资源；不要在构造或字段初始化中取得需要回收的外部资源。</summary>
        protected virtual void OnInitialize() { }

        /// <summary>清理本实例资源；初始化一旦开始，即使失败也调用一次。</summary>
        protected virtual void OnRelease() { }

        private static T Construct()
        {
            Type type = typeof(T);
            ConstructorInfo constructor = type.GetConstructor(
                BindingFlags.Instance | BindingFlags.NonPublic, null, Type.EmptyTypes, null);

            if (type.IsAbstract || constructor == null || !constructor.IsPrivate ||
                type.GetConstructors().Length != 0)
            {
                throw new InvalidOperationException(
                    type.FullName + " must be concrete, have a private parameterless constructor, and have no public constructors.");
            }

            T candidate = (T)constructor.Invoke(BindingFlags.DoNotWrapExceptions, null, null, null);
            try
            {
                candidate.OnInitialize();
                return candidate;
            }
            catch (Exception initializationError)
            {
                try
                {
                    candidate.OnRelease();
                }
                catch (Exception releaseError)
                {
                    throw new AggregateException(initializationError, releaseError);
                }

                throw;
            }
        }
    }
}
