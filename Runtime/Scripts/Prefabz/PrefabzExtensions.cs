using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace NoZ.Prefabz
{
    public static class PrefabzExtensions
    {
        public static void PooledDestroy (this GameObject gameObject)
        {
            PrefabzManager.Instance.PooledDestroy(gameObject);
        }
    }
}
