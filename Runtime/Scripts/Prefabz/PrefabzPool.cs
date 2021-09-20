using System.Collections.Generic;
using UnityEngine;

namespace NoZ.Prefabz
{
    public class PrefabzPool
    {
        private const int DefaultMaxPoolSize = 16;

        private GameObject _prefab = null;
        private List<GameObject> _pooledObjects = null;

        public GameObject prefab => _prefab;

        public int instanceID => _prefab.GetInstanceID();

        public PrefabzPool (GameObject prefab)
        {
            var prefabOptions = prefab.GetComponent<PrefabzPrefab>();

            _prefab = prefab;
            _pooledObjects = new List<GameObject>(prefabOptions != null ? prefabOptions.maxPoolSize : DefaultMaxPoolSize);
        }

        public GameObject Instantiate (Transform transform)
        {
            if(_pooledObjects.Count > 0)
            {
                var index = _pooledObjects.Count - 1;
                var go = _pooledObjects[index];
                _pooledObjects.RemoveAt(index);
                go.transform.SetParent(transform);
                go.SetActive(true);
                return go;
            }
            else
            {
                var go = GameObject.Instantiate(_prefab, transform);
                if (!go.TryGetComponent<PrefabzPrefab>(out var prefab))
                    prefab = go.AddComponent<PrefabzPrefab>();

                prefab._prefab = _prefab;

                return go;
            }
        }

        internal bool AddToPool (GameObject gameObject)
        {
            if (_pooledObjects.Count >= _pooledObjects.Capacity)
                return false;

            _pooledObjects.Add(gameObject);
            return true;
        }
    }
}
