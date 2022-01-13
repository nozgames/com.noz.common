using UnityEngine;
using UnityEngine.Events;

namespace NoZ
{
    /// <summary>
    /// Script that on `Start` will automatically initialize all singletons in the
    /// order they are specified.
    /// </summary>
    public class Startup : MonoBehaviour
    {
        [SerializeField] private Singleton[] singletons = null;
        [SerializeField] private UnityEvent onBeforeInitialize;
        [SerializeField] private UnityEvent onAfterInitialize;
        [SerializeField] private UnityEvent onBeforeShutdown;
        [SerializeField] private UnityEvent onAfterShutdown;

        void Start()
        {
            if (null == singletons)
                return;

            onBeforeInitialize?.Invoke();

            foreach (var singleton in singletons)
                singleton.Initialize();

            onAfterInitialize?.Invoke();
        }

        private void OnApplicationQuit()
        {
            if (null == singletons)
                return;

            onBeforeShutdown?.Invoke();

            foreach (var singleton in singletons)
                singleton.Shutdown();

            onAfterShutdown?.Invoke();
        }

        void OnDestroy()
        {
        }
    }
}
