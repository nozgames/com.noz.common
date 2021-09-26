#if UNITY_COLLECTIONS && UNITY_TRANSPORT

using UnityEngine;
using Unity.Networking.Transport;
using System;
using System.Collections.Generic;

namespace NoZ.Netz
{
    public unsafe class NetzManager : Singleton<NetzManager>
    {
        [SerializeField] private NetzObject[] _prefabs = null;

        private NetzClient _client;
        private NetzServer _server;
        private Dictionary<ulong, NetzObject> _prefabsByHash;

        public bool isHost => _client != null && _server != null;
        public bool isClient => _client != null;
        public bool isServer => _server != null;

        public int connectedClientCount => _server?.clientCount ?? 0;

        public uint localClientId => _client?.id ?? 0;

        public event Action onServerStarted;

        public event Action onServerStopped;

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

            NetzMessage.Shutdown();

            if (_client != null)
            {
                _client.Dispose();
                _client = null;
            }

            if(_server != null)
            {
                _server.Dispose();
                _server = null;
            }
        }

        public void StartServer ()
        {
            StartServerInternal();
            NetzObjectManager.RegisterSceneObjects();

            onServerStarted?.Invoke();
        }

        private void StartServerInternal ()
        {
            var endpoint = NetworkEndPoint.AnyIpv4;
            endpoint.Port = 9000;

            _server = NetzServer.Create(endpoint);

            // TODO: error binding to port?

        }

        public void StartHost ()
        {
            StartServerInternal();
            StartClientInternal();

            NetzObjectManager.RegisterSceneObjects();

            onServerStarted?.Invoke();
        }

        public void StartClient ()
        {
            StartClientInternal();
            NetzObjectManager.RegisterSceneObjects();
        }

        public void StartClientInternal ()
        {
            var endpoint = NetworkEndPoint.LoopbackIpv4;
            endpoint.Port = 9000;
            _client = NetzClient.Connect(endpoint);
        }

        private void Update()
        {
            if (_server != null)
                _server.Update();

            if(_client != null)
                _client.Update();
        }
    }
}

#endif