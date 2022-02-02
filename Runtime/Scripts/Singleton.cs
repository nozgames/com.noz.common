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

        public static T Instance => _instance;

        public static bool IsInitialized { get; private set; }

        protected virtual void Awake()
        {
            _instance = this as T;
        }

        protected virtual void OnDisable()
        {
            _instance = null;
        }

        public override void Initialize()
        {
            if (IsInitialized)
                return;

            IsInitialized = true;
            (_instance as Singleton<T>).OnInitialize();
        }

        public override void Shutdown()
        {
            if (!IsInitialized)
                return;

            IsInitialized = false;
            (_instance as Singleton<T>).OnShutdown();
        }
        
        protected virtual void OnInitialize() { }

        protected virtual void OnShutdown() { }
    }
}
