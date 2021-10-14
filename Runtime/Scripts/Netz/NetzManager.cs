#if UNITY_COLLECTIONS && UNITY_TRANSPORT

using UnityEngine;
using Unity.Networking.Transport;
using System;
using System.Collections.Generic;

namespace NoZ.Netz
{
    [DefaultExecutionOrder(int.MaxValue)]
    public unsafe class NetzManager : Singleton<NetzManager>
    {
        [SerializeField] internal int _updateRate = 20;
        [SerializeField] internal int _clientUpdateRate = 20;
        [SerializeField] private NetzObject[] _prefabs = null;

        private Dictionary<ulong, NetzObject> _prefabsByHash;

        public event ServerStateChangedEvent onServerStateChanged;

        public event ClientStateChangeEvent onClientStateChanged;

        public double clientUpdateInterval => 1.0 / _clientUpdateRate;

        protected override void OnInitialize()
        {
            base.OnInitialize();

            InitializePrefabs();

            // Create an object to manage server time.
            var go = new GameObject();
            go.transform.SetParent(transform);
            go.hideFlags = HideFlags.HideAndDontSave;
            go.AddComponent<NetzTimeUpdater>();
        }

        private void InitializePrefabs ()
        {
            if (_prefabs == null)
                return;

            _prefabsByHash = new Dictionary<ulong, NetzObject>();

            // Generate the prefab hashes for all known prefabs
            foreach(var prefab in _prefabs)
            {
                var hash = prefab.GeneratePrefabHash();
                if (_prefabsByHash.ContainsKey(hash))
                    throw new InvalidOperationException("Duplicate hash entries are not allowed");

                _prefabsByHash[hash] = prefab;
            }
        }

        /// <summary>
        /// Get the prefab that matches the given prefab hash
        /// </summary>
        /// <param name="prefabHash">Prefab hash</param>
        /// <returns>Prefab that matches or null if not found</returns>
        internal bool TryGetPrefab(ulong prefabHash, out NetzObject prefab) => 
            _prefabsByHash.TryGetValue(prefabHash, out prefab);

        protected override void OnShutdown()
        {
            base.OnShutdown();

            if (NetzClient.instance != null)
                NetzClient.instance.Disconnect();

            if (NetzServer.instance != null)
                NetzServer.instance.Stop();

            Debug.Assert(NetzClient.instance == null);
            Debug.Assert(NetzServer.instance == null);
        }

#if false
        public void Stop ()
        {
            if (_client != null)
                _client.Disconnect();
        }


        public void StartServer (ushort port =9000)
        {
            StartServerInternal(port);
        }

        private void StartServerInternal (ushort port)
        {
            var endpoint = NetworkEndPoint.AnyIpv4;
            endpoint.Port = port;
            _server = NetzServer.Create(endpoint);
        }

        public void StartHost (ushort port = 9000)
        {
            StartServerInternal(port);

            var endpoint = NetworkEndPoint.LoopbackIpv4;
            endpoint.Port = port;
            StartClientInternal(endpoint);
        }

        public void StartClient (NetworkEndPoint endpoint)
        {
            StartClientInternal(endpoint);
            NetzObjectManager.SpawnSceneObjects();
        }

        public void StartClientInternal (NetworkEndPoint endpoint)
        {
            _client = NetzClient.Connect(endpoint);
        }
#endif

        private void Update()
        {
            if (NetzClient.instance != null)
                NetzClient.instance.Update();
        }

        private void FixedUpdate()
        {
            // Update the server on a fixed interval
            if (NetzServer.instance != null)
                NetzServer.instance.Update();
        }

        internal void RaiseClientStateChanged (uint clientId, NetzClientState oldState, NetzClientState newState)
        {
            onClientStateChanged?.Invoke(clientId, oldState, newState);
        }

        internal void RaiseServerStateChanged(NetzServerState oldState, NetzServerState newState)
        {
            onServerStateChanged?.Invoke(oldState, newState);
        }

#if false
        /// <summary>
        /// Instantiate a network object using a custom type
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="networkInstanceId"></param>
        /// <param name="parent"></param>
        /// <returns></returns>
        /// <exception cref="System.InvalidOperationException"></exception>
        public static T Instantiate<T>(ulong networkInstanceId, Transform parent = null) where T : NetzObject
        {
            if ((networkInstanceId & NetzConstants.ObjectInstanceIdTypeMask) != NetzConstants.CustomNetworkInstanceId)
                throw new System.InvalidOperationException("Networking InstanceId must be a custom network instance id");

            if (_objectsById.ContainsKey(networkInstanceId))
                throw new System.InvalidOperationException("Custom network instance identifier is already in use");

            var gameObject = new GameObject();
            gameObject.transform.SetParent(parent);
            var t = gameObject.AddComponent<T>();
            t._networkInstanceId = networkInstanceId;

            _objectsById.Add(networkInstanceId, t);
            _objects.AddLast(t._node);

            t.NetworkStart();

            return t;
        }
#endif
    }
}

#endif