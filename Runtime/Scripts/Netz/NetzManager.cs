#if UNITY_COLLECTIONS && UNITY_TRANSPORT

using UnityEngine;
using Unity.Networking.Transport;

namespace NoZ.Netz
{
    public unsafe class NetzManager : Singleton<NetzManager>
    {
        private NetzClient _client;
        private NetzServer _server;

        public bool isHost => _client != null && _server != null;
        public bool isClient => _client != null;
        public bool isServer => _server != null;

        public int connectedClientCount => _server?.clientCount ?? 0;

        public uint localClientId => _client?.id ?? 0;

        protected override void OnInitialize()
        {
            base.OnInitialize();
        }

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
        }

        private void StartServerInternal ()
        {
            var endpoint = NetworkEndPoint.AnyIpv4;
            endpoint.Port = 9000;

            _server = NetzServer.Create(endpoint);
        }

        public void StartHost ()
        {
            StartServerInternal();
            StartClientInternal();

            NetzObjectManager.RegisterSceneObjects();
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