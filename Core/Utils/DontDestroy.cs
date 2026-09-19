using UnityEngine;

namespace GameSDK
{
    /// <summary>
    /// 切场景不自动销毁的 GameObject 
    /// </summary>
    public class GameObjectDontDestroyOnLoad : MonoBehaviour
    {
        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
        }
    }
}
