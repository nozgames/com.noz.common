#if UNITY_COLLECTIONS && UNITY_TRANSPORT

using UnityEngine;
using UnityEngine.Assertions;
using Unity.Collections;
using Unity.Networking.Transport;
using Unity.Networking.Transport.Utilities;

namespace NoZ.Netz
{
    public unsafe class NetzManager : Singleton<NetzManager>
    {
        private const float KeepAliveDuration = 1.0f;

        // TODO: reliable pipeline

        private NativeList<NetzClient> _clients;
        private NetworkDriver _serverDriver;
        private NetworkPipeline _serverReliablePipeline;
        private NetworkDriver _clientDriver;
        private NetworkPipeline _clientReliablePipeline;
        private NetzClient _localClient;
        private float _nextKeepAlive;        

        public bool IsHost { get; private set; }
        public bool IsClient { get; private set; }
        public bool IsServer { get; private set; }
        public bool IsConnected { get; private set; }

        public int connectedClientCount => _clients.Length;

        public NetzClient localClient => _localClient;

        protected override void OnInitialize()
        {
            base.OnInitialize();

            _clients = new NativeList<NetzClient>(16, Allocator.Persistent);
        }

        protected override void OnShutdown()
        {
            base.OnShutdown();

            NetzMessage.Shutdown();

            _clients.Dispose();

            if (_serverDriver.IsCreated)
                _serverDriver.Dispose();

            if (_clientDriver.IsCreated)
                _clientDriver.Dispose();
        }

        public void StartServer ()
        {
            IsServer = true;
            StartServerInternal();
        }

        private void StartServerInternal ()
        {
            _serverDriver = NetworkDriver.Create(new ReliableUtility.Parameters { WindowSize = 32 });
            _serverReliablePipeline = _serverDriver.CreatePipeline(typeof(ReliableSequencedPipelineStage));

            var endpoint = NetworkEndPoint.AnyIpv4;
            endpoint.Port = 9000;
            if (_serverDriver.Bind(endpoint) != 0)
                Debug.Log("Failed to bind to port 9000");
            else
                _serverDriver.Listen();
        }

        public void StartHost ()
        {
            StartServerInternal();
            StartClientInternal();

            IsServer = true;
            IsHost = true;

            NetzObjectManager.RegisterSceneObjects();
        }

        public void StartClient ()
        {
            IsClient = true;
            StartClientInternal();
            NetzObjectManager.RegisterSceneObjects();
        }

        public void StartClientInternal ()
        {
            _clientDriver = NetworkDriver.Create();
            _clientReliablePipeline = _clientDriver.CreatePipeline(typeof(ReliableSequencedPipelineStage));

            var endpoint = NetworkEndPoint.LoopbackIpv4;
            endpoint.Port = 9000;

            _localClient = new NetzClient(_clientDriver.Connect(endpoint));
            _nextKeepAlive = KeepAliveDuration;
        }

        private void Update()
        {
            if (IsServer || IsHost)
                UpdateServer();

            if(IsClient || IsHost)
                UpdateLocalClient();
        }

        private void UpdateLocalClient()
        {
            _clientDriver.ScheduleUpdate().Complete();

            var connection = _localClient.connection;
            if (!_localClient.connection.IsCreated)
                return;

            // Make sure we send messages periodically to the server or it will drop us
            _nextKeepAlive -= Time.deltaTime;
            if (_nextKeepAlive < 0.0f)
            {
                NetzMessage.Send(null, NetzGlobalMessages.KeepAlive, NetzMessageRouting.Server);
                _nextKeepAlive = KeepAliveDuration;
            }

            DataStreamReader stream;
            NetworkEvent.Type cmd;

            while ((cmd = connection.PopEvent(_clientDriver, out stream)) != NetworkEvent.Type.Empty)
            {
                if (cmd == NetworkEvent.Type.Connect)
                {
                    _localClient._connected = true;
                    _clients[0] = _localClient;
                }
                else if (cmd == NetworkEvent.Type.Data)
                {
                    ReadMessage(stream);
                }
                else if (cmd == NetworkEvent.Type.Disconnect)
                {
                    _localClient._connection = default(NetworkConnection);
                }
            }
        }

        private void UpdateServer()
        {
            _serverDriver.ScheduleUpdate().Complete();

            // Clean up any disconnected clients
            for (int i = 0; i < _clients.Length; i++)
            {
                if (!_clients[i].connection.IsCreated)
                {
                    _clients.RemoveAtSwapBack(i);
                    --i;
                }
            }

            // Accept new connections
            NetworkConnection c;
            while ((c = _serverDriver.Accept()) != default(NetworkConnection))
            {
                _clients.Add(new NetzClient(c) { _connected = true });

                // TODO: assign an identifier to the client
                // TODO: send the client connected message to all clients
            }

            // Read incoming data from all clients
            DataStreamReader stream;
            for (int i = 0; i < _clients.Length; i++)
            {
                var connection = _clients[i].connection;
                Assert.IsTrue(connection.IsCreated);

                NetworkEvent.Type cmd;
                while ((cmd = _serverDriver.PopEventForConnection(connection, out stream)) != NetworkEvent.Type.Empty)
                {
                    if (cmd == NetworkEvent.Type.Data)
                    {
                        ReadMessage(stream);
                    }
                    else if (cmd == NetworkEvent.Type.Disconnect)
                    {
                        _clients[i] = default(NetzClient);
                    }
                }
            }

            // Send any new snapshot for all dirty objects
            for(int i=NetzObjectManager._dirtyObjects.Count - 1; i>=0; i--)
            {
                // Clear dirty state
                var obj = NetzObjectManager._dirtyObjects[i];
                obj.isDirty = false;
                NetzObjectManager._dirtyObjects.RemoveAt(i);

                // Send snapshot to all clients
                using (obj.BuildSnapshot());
            }
        }

        private void ReadMessage (DataStreamReader reader)
        {
            // Read the network object instance identifer
            var networkInstanceId = reader.ReadULong();

            // Read the message FourCC
            var message = new FourCC(reader.ReadUInt());

            // No target, global messages
            if (networkInstanceId == 0)
                return;

            if (NetzObjectManager.TryGetObject(networkInstanceId, out var obj))
            {
                obj.HandleMessage(message, reader);
            }
        }

        internal void SendToServer (byte* bytes, int length)
        {
            _clientDriver.BeginSend(_clientReliablePipeline, _localClient.connection, out var clientWriter);
            clientWriter.WriteBytes(bytes, length);
            _clientDriver.EndSend(clientWriter);

            // Since we sent a message we can reset the keep alive
            _nextKeepAlive = KeepAliveDuration;
        }

        internal void SendToAllClients (byte* bytes, int length)
        {
            if (!IsServer)
                return;

            foreach (var client in _clients)
            {
                if (!client.isConnected)
                    continue;

                _serverDriver.BeginSend(_serverReliablePipeline, client.connection, out var clientWriter);
                clientWriter.WriteBytes(bytes, length);
                _serverDriver.EndSend(clientWriter);
            }
        }
    }
}

#endif