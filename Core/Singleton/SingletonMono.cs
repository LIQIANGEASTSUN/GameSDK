using System;
using System.Reflection;
using UnityEngine;

namespace GameSDK
{
    /// <summary>
    /// 在主线程同步创建和管理自身宿主的组件单例，不接管场景中的其他组件。
    /// 构造、初始化、释放及其触发的回调不得重入同类型获取或释放；派生类不得遮蔽 OnDestroy。
    /// </summary>
    public abstract class SingletonMono<T> : MonoBehaviour where T : SingletonMono<T>
    {
        private static T _instance;

        protected SingletonMono() { }

        public static T Instance
        {
            get
            {
                if (_instance != null)
                    return _instance;

                _instance = Construct();
                return _instance;
            }
        }

        /// <summary>释放当前实例及其宿主，不创建替代实例。</summary>
        public static void ReleaseInstance()
        {
            T instance = _instance;
            if (instance == null)
                return;

            GameObject host = instance.gameObject;
            _instance = null;
            try
            {
                instance.OnRelease();
            }
            finally
            {
                if (host != null)
                    Destroy(host);
            }
        }

        protected virtual void OnInitialize() { }
        protected virtual void OnRelease() { }

        protected void OnDestroy()
        {
            if (!ReferenceEquals(_instance, this))
                return;

            GameObject host = gameObject;
            _instance = null;
            try
            {
                OnRelease();
            }
            catch (Exception error)
            {
                Debug.LogException(error, this);
            }
            finally
            {
                if (host != null)
                    Destroy(host);
            }
        }

        private static T Construct()
        {
            Type type = typeof(T);
            ConstructorInfo constructor = type.GetConstructor(
                BindingFlags.Instance | BindingFlags.NonPublic, null, Type.EmptyTypes, null);
            if (type.IsAbstract || constructor == null || !constructor.IsPrivate || type.GetConstructors().Length != 0)
                throw new InvalidOperationException($"{type.FullName} must be concrete, have a private parameterless constructor, and expose no public constructors.");

            GameObject host = new GameObject(type.Name);
            T candidate = null;
            try
            {
                host.SetActive(false);
                candidate = host.AddComponent<T>();
                candidate.OnInitialize();
                host.SetActive(true);
                if (candidate == null || !host.activeInHierarchy)
                    throw new InvalidOperationException($"{type.FullName} must remain valid and active in the hierarchy after initialization.");
                return candidate;
            }
            catch (Exception original)
            {
                try
                {
                    if (!ReferenceEquals(candidate, null))
                        candidate.OnRelease();
                }
                catch (Exception cleanup)
                {
                    throw new AggregateException(original, cleanup);
                }
                finally
                {
                    Destroy(host);
                }
                throw;
            }
        }
    }
}
