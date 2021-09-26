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
        public static NetzObject Spawn (NetzObject prefab, NetzObject parent, uint ownerClientId)
        {
            // TODO: if this is the client we have to send a message to the server to spawn the object
            if (!NetzManager.instance.isServer)
                return null;

            // Instantiate the actual game object
            var go = Object.Instantiate(prefab.gameObject, parent == null ? null : parent.transform);
            var netobj = go.GetComponent<NetzObject>();
            if(null == netobj)
            {
                Object.Destroy(go);
                return null;
            }

            netobj._networkInstanceId = _nextSpawnedObjectInstanceId++;
            netobj.prefabHash = prefab.prefabHash;
            netobj.state = NetzObjectState.Spawning;
            netobj.ownerClientId = ownerClientId;

            // Track the object
            _objects.Add(netobj._networkInstanceId, netobj);

            netobj.NetworkStart();

            return netobj;
        }

        internal static NetzObject SpawnOnClient(ulong prefabHash, uint ownerClientId, ulong networkInstanceId)
        {
            if (!NetzManager.instance.TryGetPrefab(prefabHash, out var prefab))
            {
                Debug.LogError($"Unknown prefab hash `{prefabHash}`.  Make sure the prefab is included in the NetzManager prefab list.");
                return null;
            }

            // TODO: parent
            var netobj = Object.Instantiate(prefab.gameObject).GetComponent<NetzObject>();
            netobj._networkInstanceId = networkInstanceId;
            netobj.ownerClientId = ownerClientId;

            // Track the object
            _objects.Add(netobj._networkInstanceId, netobj);

            netobj.NetworkStart();

            return netobj;
        }

        /// <summary>
        /// Despawn an object from the server and al clients
        /// </summary>
        /// <param name=""></param>
        /// <param name=""></param>
        public static void Despawn (NetzObject netobj)
        {
            // Despawn must be called on the server
            if (!NetzManager.instance.isServer)
                return;

            // Mark the object as despawned
            netobj.state = NetzObjectState.Despawning;

            // Add to the dirty list to make sure the object gets despawned
            if (netobj._dirtyNode.List == null)
                _dirtyObjects.AddLast(netobj._dirtyNode);

            // Remove from the tracked object list
            _objects.Remove(netobj.networkInstanceId);

            netobj.OnDespawn();

            // Disable the object so it does not think until it can be despawned
            netobj.gameObject.SetActive(false);
        }

        internal static void DespawnOnClient (ulong networkInstanceId)
        {
            if (!NetzManager.instance.isClient || NetzManager.instance.isHost)
                return;

            if (!TryGetObject(networkInstanceId, out var netobj))
                return;

            _objects.Remove(networkInstanceId);

            netobj.OnDespawn();

            Object.Destroy(netobj.gameObject);
        }
    }
}

#endif