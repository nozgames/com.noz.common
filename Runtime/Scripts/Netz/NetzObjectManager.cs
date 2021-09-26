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
        internal static ulong _nextSpawnedObjectInstanceId = NetzConstants.SpawnedObjectInstanceId;

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

        public static T SpawnCustom<T> (ulong networkInstanceId, Transform parent) where T : NetzObject
        {
            if ((networkInstanceId & NetzConstants.ObjectInstanceIdTypeMask) != NetzConstants.CustomNetworkInstanceId)
                throw new System.InvalidOperationException("Networking InstanceId must be a custom network instance id");

            if(_objects.ContainsKey(networkInstanceId))
                throw new System.InvalidOperationException("Custom network instance identifier is already in use");

            var gameObject = new GameObject();
            gameObject.transform.SetParent(parent);
            var t = gameObject.AddComponent<T>();
            t._networkInstanceId = networkInstanceId;
            t.state = NetzObjectState.Spawned;

            _objects.Add(networkInstanceId, t);

            return t;
        }

        /// <summary>
        /// Spawn a network object 
        /// </summary>
        /// <param name="prefab"></param>
        /// <param name="parent"></param>
        public static void Spawn (NetzObject prefab, NetzObject parent, uint ownerClientId)
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

        internal static NetzObject SpawnOnClient (ulong prefabHash, ulong networkInstanceId)
        {
            if (!NetzManager.instance.TryGetPrefab(prefabHash, out var prefab))
                return null;

            // TODO: parent
            var netobj = Object.Instantiate(prefab.gameObject).GetComponent<NetzObject>();
            netobj._networkInstanceId = networkInstanceId;

            // Track the object
            _objects.Add(netobj._networkInstanceId, netobj);

            return netobj;
        }
    }
}

#endif