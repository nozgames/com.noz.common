using UnityEngine;

namespace NoZ
{
    public abstract class Singleton : MonoBehaviour
    {
        public abstract void Initialize();

        public abstract void Shutdown();
    }

    public abstract class Singleton<T> : Singleton where T : class
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

        public override void Initialize() => (_instance as Singleton<T>).OnInitialize();

        public override void Shutdown() => (_instance as Singleton<T>).OnShutdown();

        protected virtual void OnInitialize() { }

        protected virtual void OnShutdown() { }
    }
}
