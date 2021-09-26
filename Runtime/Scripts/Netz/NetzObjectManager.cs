#if UNITY_COLLECTIONS && UNITY_TRANSPORT

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace NoZ.Netz
{
    /// <summary>
    /// Static class that manages all network objects
    /// </summary>
    public static class NetzObjectManager
    {
        internal const ulong FirstSpawnedObjectInstanceId = 1 << 24;

        internal static ulong _nextSpawnedObjectInstanceId = FirstSpawnedObjectInstanceId;

        internal static readonly Dictionary<ulong, NetzObject> _objects = new Dictionary<ulong, NetzObject>();

        internal static readonly LinkedList<NetzObject> _dirtyObjects = new LinkedList<NetzObject>();

        public static bool TryGetObject (ulong networkInstanceId, out NetzObject obj) =>
            _objects.TryGetValue(networkInstanceId, out obj);

        internal static void RegisterSceneObjects ()
        {
            foreach(var obj in Object.FindObjectsOfType<NetzObject>())
            {
                _objects.Add(obj.networkInstanceId, obj);
            }
        }

        /// <summary>
        /// Spawn a network object 
        /// </summary>
        /// <param name="prefab"></param>
        /// <param name="parent"></param>
        public static void Spawn (NetzObject prefab, NetzObject parent)
        {
            // TODO: if this is the client we have to send a message to the server to spawn the object
            if (!NetzManager.instance.isServer)
                return;

            // Instantiate the actual game object
            var go = Object.Instantiate(prefab.gameObject, parent == null ? null : parent.transform);
            var netobj = go.GetComponent<NetzObject>();
            if(null == netobj)
            {
                Object.Destroy(go);
                return;
            }

            netobj._networkInstanceId = _nextSpawnedObjectInstanceId++;
            netobj.prefabHash = prefab.prefabHash;
            netobj.state = NetzObjectState.Spawning;

            // Track the object
            _objects.Add(netobj._networkInstanceId, netobj);
        }

        internal static NetzObject SpawnOnClient (ulong prefabHash)
        {
            if (!NetzManager.instance.TryGetPrefab(prefabHash, out var prefab))
                return null;

            // TODO: parent
            return Object.Instantiate(prefab.gameObject).GetComponent<NetzObject>();
        }
    }
}

#endif