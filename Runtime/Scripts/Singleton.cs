using UnityEngine;

namespace NoZ
{
    public class Singleton<T> : MonoBehaviour where T : class
    {
        private static T _instance = null;

        public static T instance => _instance;

        protected virtual void Awake()
        {
            _instance = this as T;
        }

        protected virtual void OnDisable()
        {
            _instance = null;
        }

        public static void Initialize() => (_instance as Singleton<T>).OnInitialize();

        public static void Shutdown() => (_instance as Singleton<T>).OnShutdown();

        protected virtual void OnInitialize() { }

        protected virtual void OnShutdown() { }
    }
}
