using UnityEngine;

namespace NoZ
{
    /// <summary>
    /// Script that on `Start` will automatically initialize all singletons in the
    /// order they are specified.
    /// </summary>
    public class Startup : MonoBehaviour
    {
        [SerializeField] private Singleton[] singletons = null;

        void Start()
        {
            if (null == singletons)
                return;

            foreach (var singleton in singletons)
                singleton.Initialize();
        }

        private void OnApplicationQuit()
        {
            if (null == singletons)
                return;

            foreach (var singleton in singletons)
                singleton.Shutdown();
        }

        void OnDestroy()
        {
        }
    }
}
