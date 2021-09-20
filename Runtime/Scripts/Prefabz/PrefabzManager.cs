using System.Collections.Generic;
using UnityEngine;

namespace NoZ.Prefabz
{
    public class PrefabzManager : Singleton<PrefabzManager>
    {
        private Dictionary<int, PrefabzPool> _pools = new Dictionary<int, PrefabzPool>();

        internal Transform _pooled = null;

        protected override void OnInitialize()
        {
            base.OnInitialize();

            var go = new GameObject();
            go.name = "PrefabzPool";
            go.transform.SetParent(transform);
            go.hideFlags = HideFlags.HideAndDontSave;
            go.SetActive(false);
            _pooled = go.transform;
        }

        public PrefabzPool GetPool (GameObject prefab)
        {
            var instanceID = prefab.GetInstanceID();
            if (!_pools.TryGetValue(instanceID, out var pool))
            {
                pool = new PrefabzPool(prefab);
                _pools[instanceID] = pool;
            }

            return pool;
        }

        public bool TryGetPool(GameObject prefab, out PrefabzPool pool)
        {
            pool = GetPool(prefab);
            return pool != null;
        }

        public void PooledDestroy(GameObject gameObject)
        {
            var prefab = gameObject.GetComponent<PrefabzPrefab>();
            if(null == prefab || !TryGetPool(prefab._prefab, out var pool) || !pool.AddToPool(gameObject))
            {
                Destroy(gameObject);
                return;
            }

            // Disable the game object and move it too the pooled transform
            gameObject.SetActive(false);
            gameObject.transform.SetParent(_pooled);
        }

        public GameObject PooledInstantitate (GameObject prefab, Transform transform = null)
        {
            if (!TryGetPool(prefab, out var pool))
                return Instantiate(prefab, transform);

            return pool.Instantiate(transform);
        }

        public T PooledInstantitate<T>(GameObject prefab, Transform transform = null)
        {
            if (!TryGetPool(prefab, out var pool))
                return Instantiate(prefab, transform).GetComponent<T>();

            return pool.Instantiate(transform).GetComponent<T>();
        }
    }
}
