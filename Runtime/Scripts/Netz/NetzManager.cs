#if UNITY_COLLECTIONS && UNITY_TRANSPORT

using UnityEngine;
using Unity.Networking.Transport;
using System;
using System.Collections.Generic;

namespace NoZ.Netz
{
    public unsafe class NetzManager : Singleton<NetzManager>
    {
        [SerializeField] internal int _updateRate = 20;
        [SerializeField] private NetzObject[] _prefabs = null;

        private Dictionary<ulong, NetzObject> _prefabsByHash;

        public bool isHost => NetzClient.instance != null && NetzServer.instance != null;
        public bool isClient => NetzClient.instance != null && NetzServer.instance == null;
        public bool isServer => NetzClient.instance == null && NetzServer.instance != null;
        public bool isServerOrHost => isServer || isHost;

        public event ServerStateChangedEvent onServerStateChanged;

        public event ClientStateChangeEvent onClientStateChanged;

        protected override void OnInitialize()
        {
            base.OnInitialize();

            InitializePrefabs();
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

            NetzMessage.Shutdown();
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

        /// <summary>
        /// Load the given scene
        /// </summary>
        /// <param name="sceneName"></param>
        public Coroutine LoadSceneAsync (string sceneName)
        {
            if (!isServerOrHost)
                throw new InvalidOperationException("LoadSceneAsync can only be called on the server");

            return NetzServer.instance.LoadSceneAsync(sceneName);
        }

        private void Update()
        {
            if (NetzServer.instance != null)
                NetzServer.instance.Update();

            if (NetzClient.instance != null)
                NetzClient.instance.Update();
        }

        internal void RaiseClientStateChanged (uint clientId, NetzClientState oldState, NetzClientState newState)
        {
            onClientStateChanged?.Invoke(clientId, oldState, newState);
        }

        internal void RaiseServerStateChanged(NetzServerState oldState, NetzServerState newState)
        {
            onServerStateChanged?.Invoke(oldState, newState);
        }
    }
}

#endif