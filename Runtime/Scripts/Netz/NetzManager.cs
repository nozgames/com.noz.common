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

        private NetzClient _client;
        private NetzServer _server;
        private Dictionary<ulong, NetzObject> _prefabsByHash;

        public bool isHost => _client != null && _server != null;
        public bool isClient => _client != null;
        public bool isServer => _server != null;
        public bool isServerOrHost => isServer || isHost;

        public NetzServerState serverState => _server?.state ?? NetzServerState.Unknown;

        public NetzClientState clientState => _client?.state ?? NetzClientState.Unknown;

        public int connectedClientCount => _server?.clientCount ?? 0;

        public uint localClientId => _client?.id ?? 0;

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

            if (_client != null)
            {
                _client.Dispose();
                _client = null;
            }

            if (_server != null)
            {
                _server.Dispose();
                _server = null;
            }

            NetzMessage.Shutdown();
        }

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

        /// <summary>
        /// Load the given scene
        /// </summary>
        /// <param name="sceneName"></param>
        public Coroutine LoadSceneAsync (string sceneName)
        {
            if (!isServerOrHost)
                throw new InvalidOperationException("LoadSceneAsync can only be called on the server");

            return _server.LoadSceneAsync(sceneName);
        }

        private void Update()
        {
            if (_server != null)
                _server.Update();

            if (_client != null)
            {
                _client.Update();

                if (_client.state == NetzClientState.Disconnected)
                    _client = null;
            }
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