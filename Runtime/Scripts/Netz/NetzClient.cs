#if UNITY_COLLECTIONS && UNITY_TRANSPORT

using UnityEngine;
using Unity.Networking.Transport;
using System;
using System.Collections.Generic;
using Unity.Networking.Transport.Utilities;
using System.Collections;
using UnityEngine.SceneManagement;

namespace NoZ.Netz
{
    public unsafe class NetzClient
    {
        private const float KeepAliveDuration = 10.0f;

        private class ConnectedPlayer
        {
            public uint id;
            public NetzClientState oldState;
            public NetzClientState state;
            public NetzPlayer player;
        }

        private NetworkConnection _connection;
        private NetworkDriver _driver;
        private NetworkPipeline _pipeline;
        private NetzPlayer _player;
        private string _scene;
        private float _nextKeepAlive;
        private double _lastMessageSendTime;
        private double _lastMessageReceiveTime;
        private NetzMessageRouter<NetzClient> _router;
        private Dictionary<uint, ConnectedPlayer> _connectedClients = new Dictionary<uint, ConnectedPlayer>();


#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private NetzDebuggerClient _debugger;
#endif

        public static NetzClient instance { get; private set; }
        public static bool isCreated => instance != null;

        public NetworkConnection connection => _connection;

        /// <summary>
        /// True if the client is a host client
        /// </summary>
        public bool isHost { get; private set; }

        public static event Action<NetzPlayer> onPlayerConnected;
        public static event Action<NetzPlayer> onPlayerDisconnected;

        private double timeSinceLastMessageSent => Time.realtimeSinceStartupAsDouble - _lastMessageSendTime;
        private double timeSinceLastMessageReceived => Time.realtimeSinceStartupAsDouble - _lastMessageReceiveTime;

        /// <summary>
        /// Unique identifier of the client
        /// </summary>
        public uint id { get; private set; }

        /// <summary>
        /// Current state of the client
        /// </summary>
        public NetzClientState state { get; private set; }

        /// <summary>
        /// True if the client is connected to the server and has finished the connection handshake
        /// </summary>
        public bool isConnected => id != 0;

        /// <summary>
        /// Last snapshot number that was sent to the client
        /// </summary>
        internal int snapshotSent = 0;

        /// <summary>
        /// Last snapshot number that was received by the client
        /// </summary>
        internal int snapshotReceived = 0;


        internal NetzClient(NetzPlayer player)
        {
            _player = player;
            _driver = NetworkDriver.Create(new FragmentationUtility.Parameters { PayloadCapacity = NetzConstants.MaxMessageSize });
            _pipeline = _driver.CreatePipeline(typeof(FragmentationPipelineStage));
            _nextKeepAlive = KeepAliveDuration;
            state = NetzClientState.Connecting;

            _router = new NetzMessageRouter<NetzClient>();
            _router.AddRoute(NetzConstants.Messages.Connect, OnConnectMessage);
            _router.AddRoute(NetzConstants.Messages.Disconnect, OnDisconnectMessage);
            _router.AddRoute(NetzConstants.Messages.Spawn, OnSpawnMessage);
            _router.AddRoute(NetzConstants.Messages.ClientStates, OnClientStates);
            _router.AddRoute(NetzConstants.Messages.Synchronize, OnSynchronizeMessage);
            _router.AddRoute(NetzConstants.Messages.Snapshot, OnSnapshotMessage);
        }

        public static void Connect(NetworkEndPoint endpoint, NetzPlayer player)
        {
            if (instance != null)
                throw new InvalidOperationException("Only one client can be connected at a time");

            // If a server is running then ensure the endpoint is the running server as a client must connect
            // to the running server if there is one.
            if(NetzServer.instance != null && (!endpoint.IsLoopback || endpoint.Port != NetzServer.instance.port))
                throw new InvalidOperationException("Client can only be connected as host when server is running");

            instance = new NetzClient(player);
            instance.isHost = NetzServer.instance != null;
            instance._connection = instance._driver.Connect(endpoint);
            instance._player = player;
        }

        /// <summary>
        /// Connect directly to a server running in the same process.  This is generally used for
        /// clients that are acting as the host
        /// </summary>
        /// <param name="server">Server to connect to</param>
        /// <param name="player">Player</param>
        /// <returns>Client</returns>
        public static void Connect(NetzPlayer player)
        {
            if (null == NetzServer.instance)
                throw new InvalidOperationException("No server is running");

            if (null == player)
                throw new ArgumentNullException("player");

            // Connect to the internal server
            var endpoint = NetworkEndPoint.LoopbackIpv4;
            endpoint.Port = NetzServer.instance.port;
            Connect(endpoint, player);
        }

        public void Disconnect()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (_debugger != null)
            {
                _debugger.Dispose();
                _debugger = null;
            }
#endif

            // Issue a disconnect to the server
            if (_connection.IsCreated)
            {
                _connection.Disconnect(_driver);
                _connection = default(NetworkConnection);
            }

            if (_driver.IsCreated)
            {
                _driver.Dispose();
                _driver = default(NetworkDriver);
            }

            instance = null;
        }

        internal void Update()
        {
            if (!_driver.IsCreated)
                return;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (_debugger != null)
                _debugger.Update();
#endif

            _driver.ScheduleUpdate().Complete();

            if (!_connection.IsCreated)
                return;

            SendKeepAlive();

            NetworkEvent.Type cmd;
            while ((cmd = connection.PopEvent(_driver, out var stream)) != NetworkEvent.Type.Empty)
            {
                switch (cmd)
                {
                    case NetworkEvent.Type.Connect:
                        // TODO: we want to be in the synchronizing state post connect until we 
                        //       are told we are fully synchronized.
                        // state = NetzClientState.Synchronizing;
                        break;

                    case NetworkEvent.Type.Data:
                        ReadMessage(stream);
                        break;

                    case NetworkEvent.Type.Disconnect:
                        Disconnect();
                        return;
                }
            }

            switch (state)
            {
                case NetzClientState.Synchronizing: UpdateStateSynchronizing(); break;
                case NetzClientState.Synchronized: UpdateStateSynchronized(); break;
                case NetzClientState.Disconnecting: UpdateStateDisconnecting(); break;
            }
        }

        private void UpdateStateSynchronizing()
        {
            // TODO: send keepalive
        }

        private void UpdateStateSynchronized()
        {
            // Throttle
            if (timeSinceLastMessageSent < NetzConstants.SynchronizeInterval)
                return;

            // Send the synchronize ACK until the server gets it
            using (var msg = NetzMessage.Create(null, NetzConstants.Messages.Synchronize))
            {
                SendToServer(msg);
            }
        }

        private void UpdateStateDisconnecting()
        {

        }

        private void ReadMessage(DataStreamReader reader)
        {
            // Read the network object instance identifer
            var networkInstanceId = reader.ReadULong();

            // Read the message FourCC
            var messageId = new FourCC(reader.ReadUInt());

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // Ignore connect message for debugger becuase it will send to the debugger itself
            if (messageId != NetzConstants.Messages.Connect)
                SendToDebugger(messageId, received: true, length: reader.Length);
#endif

            // Global messages go through the router
            if (networkInstanceId == 0)
                _router.Route(messageId, this, ref reader);
            // Messages for objects get routed to the object
            else if (NetzObjectManager.TryGetObject(networkInstanceId, out var obj))
                obj.HandleMessage(messageId, ref reader);
        }

        private void OnConnectMessage(FourCC messageId, NetzClient client, ref DataStreamReader reader)
        {
            id = reader.ReadUInt();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // We have to special case connect message for the debugger because we want the client
            // identifier to be filled in before we send to the debugger.
            SendToDebugger(messageId, received: true, length: reader.Length);
#endif

            // Respond with an ack that contains the serialized player info
            using (var message = NetzMessage.Create(null, NetzConstants.Messages.Connect))
            {
                var writer = message.BeginWrite();
                writer.WriteUInt(id);
                _player.Serialize(ref writer);
                message.EndWrite(writer);

                SendToServer(message);
            }

            state = NetzClientState.Connected;

            // Raise player connected event for ourself
            onPlayerConnected?.Invoke(_player);
        }

        private void OnDisconnectMessage(FourCC messageType, NetzClient target, ref DataStreamReader reader)
        {
            state = NetzClientState.Disconnected;
        }

        /// <summary>
        /// Handles incoming object spawn messages
        /// </summary>
        private void OnSpawnMessage(FourCC messageId, NetzClient target, ref DataStreamReader reader)
        {
            var prefabHash = reader.ReadULong();
            var ownerClientId = reader.ReadUInt();
            var networkInstanceId = reader.ReadULong();
            var netzObject = NetzObjectManager.SpawnOnClient(prefabHash, ownerClientId: ownerClientId, networkInstanceId: networkInstanceId);
            if (null == netzObject)
                return;

            reader.ReadTransform(netzObject.transform);
        }

        private void OnSynchronizeMessage(FourCC messageId, NetzClient target, ref DataStreamReader reader)
        {
            // Ignore any other sync messages while we are already synchronizing.  
            // TODO: we need to handle the situation where the server started a new scene, the message should
            //       contain some sort of server instance id
            if (state == NetzClientState.Synchronizing)
                return;

            state = NetzClientState.Synchronizing;

            var sceneName = reader.ReadFixedString32().Value;

            // If this client is a host then they are already synchronized 
            if (isHost)
            {
                // Adjust the last message send time to force a message to be sent on the next update
                _lastMessageSendTime = Time.realtimeSinceStartup - NetzConstants.SynchronizeInterval;

                state = NetzClientState.Synchronized;
                return;
            }

            LoadSceneAsync(sceneName);
        }

        public Coroutine LoadSceneAsync(string sceneName)
        {
            IEnumerator LoadCoroutine()
            {
                // Unload the old scene
                if (_scene != null)
                {
                    yield return SceneManager.UnloadSceneAsync(_scene);
                    _scene = null;
                }

                // Load the scene and wait for it to finish to start snapshots back up
                yield return SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);

                _scene = sceneName;

                // Register and start all of the scene objects
                NetzObjectManager.SpawnSceneObjects();

                // Adjust the last message send time to force a message to be sent on the next update
                _lastMessageSendTime = Time.realtimeSinceStartup - NetzConstants.SynchronizeInterval;

                // Move to the synchronized state
                state = NetzClientState.Synchronized;
            }

            // Start a coroutine to load the state
            return NetzManager.instance.StartCoroutine(LoadCoroutine());
        }

        private void OnSnapshotMessage(FourCC messageId, NetzClient target, ref DataStreamReader reader)
        {
            // Become active one we get our first snapshot.  This is the server acknowledging that we
            // are synchronized and can continue
            if (state == NetzClientState.Synchronized)
                state = NetzClientState.Active;

            SendKeepAlive();
        }

        /// <summary>
        /// Handles incoming client state changes
        /// </summary>
        private void OnClientStates(FourCC messageId, NetzClient target, ref DataStreamReader reader)
        {
            // First mark all connected clients as disconnected assuming their state will be updated
            // in the loop below
            foreach (var connectedClient in _connectedClients.Values)
                connectedClient.state = NetzClientState.Disconnected;

            var count = (int)reader.ReadByte();
            for (var clientIndex = 0; clientIndex < count; clientIndex++)
            {
                var clientId = reader.ReadUInt();
                var state = (NetzClientState)reader.ReadByte();

                if (!_connectedClients.TryGetValue(clientId, out var connectedClient))
                    _connectedClients.Add(clientId, new ConnectedPlayer { id = clientId, oldState = NetzClientState.Unknown, state = state });
                else
                    connectedClient.state = state;
            }

            // Send events for any state changes
            foreach (var connectedClient in _connectedClients.Values)
                if (connectedClient.oldState != connectedClient.state)
                {
                    var oldState = connectedClient.oldState;
                    connectedClient.oldState = connectedClient.state;
                    NetzManager.instance.RaiseClientStateChanged(connectedClient.id, oldState, connectedClient.state);
                }
        }

        /// <summary>
        /// Send a keep alive message to the server to ensure that the connection isnt closed.
        /// </summary>
        private void SendKeepAlive()
        {
            if (state != NetzClientState.Connected)
                return;

            _nextKeepAlive -= Time.deltaTime;
            if (_nextKeepAlive > 0.0f)
                return;

            using (var message = NetzMessage.Create(null, NetzConstants.Messages.KeepAlive))
                SendToServer(message);
        }

        /// <summary>
        /// Send the given message to the server
        /// </summary>
        /// <param name="message">Message to send</param>
        internal void SendToServer(NetzMessage message)
        {
            // TODO: should we queue messages into a single send?

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            SendToDebugger(message.id, received: false, length: message.length);
#endif

            _lastMessageSendTime = Time.realtimeSinceStartupAsDouble;

            _driver.BeginSend(_pipeline, connection, out var clientWriter);
            clientWriter.WriteBytes(message.buffer, message.length);
            _driver.EndSend(clientWriter);

            // Since we sent a message we can reset the keep alive
            _nextKeepAlive = KeepAliveDuration;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private void SendToDebugger(FourCC messageId, int length, bool received)
        {
            // Create the debugger if it is not there already
            if (_debugger == null)
            {
                var debuggerEndpoint = NetworkEndPoint.LoopbackIpv4;
                debuggerEndpoint.Port = 9191;
                _debugger = NetzDebuggerClient.Connect(debuggerEndpoint, id);
            }

            _debugger.Send(from: id, to: 0, messageId, received: received, length: length);
        }
#endif
    }
}

#endif
