using UnityEngine;

namespace NoZ.Prefabz
{
    public class PrefabzPrefab : MonoBehaviour
    {
        [SerializeField] private int _maxPoolSize = 16;

        internal GameObject _prefab = null;

        public int maxPoolSize => _maxPoolSize;
    }
}
