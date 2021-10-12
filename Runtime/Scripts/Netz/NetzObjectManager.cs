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

        /// <summary>
        /// Dictionary of network id to object
        /// </summary>
        internal static readonly Dictionary<ulong, NetzObject> _objectsById = new Dictionary<ulong, NetzObject>();

        /// <summary>
        /// Linked list of all spawned objects
        /// </summary>
        internal static readonly LinkedList<NetzObject> _objects = new LinkedList<NetzObject>();

        public static bool TryGetObject (ulong networkInstanceId, out NetzObject obj) =>
            _objectsById.TryGetValue(networkInstanceId, out obj);

        internal static void SpawnSceneObjects ()
        {
            var sceneObjects = Object.FindObjectsOfType<NetzObject>();
            foreach (var netobj in sceneObjects)
            {
                _objectsById.Add(netobj.networkInstanceId, netobj);
                _objects.AddFirst(netobj._node);
            }

            foreach (var obj in sceneObjects)
                obj.NetworkStart();
        }

        public static T SpawnCustom<T> (ulong networkInstanceId, Transform parent) where T : NetzObject
        {
            if ((networkInstanceId & NetzConstants.ObjectInstanceIdTypeMask) != NetzConstants.CustomNetworkInstanceId)
                throw new System.InvalidOperationException("Networking InstanceId must be a custom network instance id");

            if(_objectsById.ContainsKey(networkInstanceId))
                throw new System.InvalidOperationException("Custom network instance identifier is already in use");

            var gameObject = new GameObject();
            gameObject.transform.SetParent(parent);
            var t = gameObject.AddComponent<T>();
            t._networkInstanceId = networkInstanceId;

            _objectsById.Add(networkInstanceId, t);
            _objects.AddLast(t._node);

            if(NetzServer.isCreated)
                t._changedInSnapshot = NetzServer.instance.currentSnapshotId;

            t.NetworkStart();

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
            if (!NetzServer.isCreated)
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
            netobj.ownerClientId = ownerClientId;
            netobj._changedInSnapshot = NetzServer.instance.currentSnapshotId;

            // Track the object
            _objectsById.Add(netobj._networkInstanceId, netobj);
            _objects.AddFirst(netobj._node);

            netobj.NetworkStart();

            NetzServer.instance.SendSpawnEvent(netobj);

            return netobj;
        }

        internal static NetzObject SpawnOnClient(ulong prefabHash, uint ownerClientId, ulong networkInstanceId, ref NetzReader reader)
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
            _objectsById.Add(netobj._networkInstanceId, netobj);
            _objects.AddFirst(netobj._node);

            netobj.Read(ref reader);

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
            if (!NetzServer.isCreated)
                return;

            // Remove from the tracked object list
            _objectsById.Remove(netobj.networkInstanceId);
            _objects.Remove(netobj._node);

            netobj.OnDespawn();

            // TODO: if this is a scene object we need to track the destroy

            NetzServer.instance.SendDespawnEvent(netobj);

            // Destroy the game object
            Object.Destroy(netobj.gameObject);
        }

        internal static void DespawnOnClient (ulong networkInstanceId)
        {
            if (!NetzServer.isCreated && NetzServer.isCreated)
                return;

            if (!TryGetObject(networkInstanceId, out var netobj))
                return;

            _objectsById.Remove(networkInstanceId);
            _objects.Remove(netobj._node);

            netobj.OnDespawn();

            Object.Destroy(netobj.gameObject);
        }

        internal static void MarkChanged (NetzObject netobj)
        {
            if (netobj._node.List == null)
                return;

            // Move the changed object to the front of the list
            _objects.Remove(netobj._node);
            _objects.AddFirst(netobj._node);

            // Snapshot this object was changed in
            netobj._changedInSnapshot = NetzServer.instance.currentSnapshotId;
        }
    }
}

#endif