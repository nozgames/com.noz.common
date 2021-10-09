#if UNITY_COLLECTIONS && UNITY_TRANSPORT

using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Networking.Transport;
using Unity.Networking.Transport.Utilities;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.SceneManagement;

namespace NoZ.Netz
{
    public unsafe class NetzServer
    {
        private class ConnectedClient
        {
            public NetworkConnection connection;
            public NetzClientState oldState;
            public NetzClientState state;
            public int lastSnapshotSent;
            public int lastSnapshotReceived;
            public uint id;
            public double lastMessageSendTime;
            public double lastMessageReceiveTime;
            public NetzPlayer player;

            public double timeSinceLastMessageSent => Time.realtimeSinceStartupAsDouble - lastMessageSendTime;
            public double timeSinceLastMessageReceived => Time.realtimeSinceStartupAsDouble - lastMessageReceiveTime;
        }

        private NetworkEndPoint _endpoint;
        private NetworkDriver _driver;
        private NetworkPipeline _pipeline;
        private List<ConnectedClient> _clients;
        private uint _nextClientId = 1;
        private NetzMessageRouter<ConnectedClient> _router;
        private float _snapshotInterval = 1.0f / 20.0f;
        private float _snapshotElapsed = 0.0f;
        private string _scene;
        private Type _playerType;
        private NetzServerState _state = NetzServerState.Unknown;

        /// <summary>
        /// Identifier of the current snapshot
        /// </summary>
        private uint _snapshotId = 1;

        /// <summary>
        /// Snapshot the client state was last changed in
        /// </summary>
        private bool _clientStateDirty;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private NetzDebuggerClient _debugger;
#endif

        public static NetzServer instance { get; private set; }
        public static bool isCreated => instance != null;

        /// <summary>
        /// Number of clients connected to the server
        /// </summary>
        public int clientCount => _clients.Count;

        public NetzServerState state => _state;

        public int MaxClients { get; set; } = 16;

        public ushort port => _endpoint.Port;

        public int playerCount => _clients.Count;

        private NetzServer(ushort port, Type playerType)
        {
            if (null == playerType)
                throw new ArgumentNullException("playerType");

            if (!typeof(NetzPlayer).IsAssignableFrom(playerType))
                throw new InvalidOperationException("playerType must be derived from NetzPlayer");

            _playerType = playerType;
            _clients = new List<ConnectedClient>(16);
            _driver = NetworkDriver.Create(new FragmentationUtility.Parameters { PayloadCapacity = NetzConstants.MaxMessageSize });
            _pipeline = _driver.CreatePipeline(typeof(FragmentationPipelineStage));

            // Initialize the message router
            _router.AddRoute(NetzConstants.Messages.Connect, OnClientConnectAck);
            _router.AddRoute(NetzConstants.Messages.Disconnect, OnClientDisconnect);
            _router.AddRoute(NetzConstants.Messages.Synchronize, OnSynchronizeAck);

            _endpoint = NetworkEndPoint.LoopbackIpv4;
            _endpoint.Port = port;
            _snapshotInterval = 1.0f / NetzManager.instance._updateRate;

            // Bind the the port
            if (_driver.Bind(_endpoint) != 0)
                return;

            // Listen for new connections
            if (0 != _driver.Listen())
                return;

            // Server is idle until a scene is loaded
            SetState(NetzServerState.Idle);
        }

        /// <summary>
        /// Create a server
        /// </summary>
        /// <param name="port">Port to run server on</param>
        public static void Start(ushort port, Type playerType)
        {
            if (instance != null)
                throw new InvalidOperationException("Only one server can be created at a time");

            if (NetzClient.instance != null)
                throw new InvalidOperationException("Server must be started before client");

            instance = new NetzServer(port, playerType);
            if (instance.state != NetzServerState.Idle)
                instance.Stop();
        }

        public void Stop()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (_debugger != null)
            {
                _debugger.Dispose();
                _debugger = null;
            }
#endif

            if (_driver.IsCreated)
            {
                foreach (var client in _clients)
                    client.connection.Disconnect(_driver);

                _driver.Dispose();
            }

            instance = null;
        }

        private void SetState (NetzServerState state)
        {
            var old = _state;
            _state = state;
            NetzManager.instance.RaiseServerStateChanged(old, state);
        }

        public NetzPlayer GetPlayer(int index) => _clients[index].player;
        public NetzClientState GetClientState(int index) => _clients[index].state;

        public T GetPlayer<T>(int index) where T : NetzPlayer => _clients[index].player as T;

        /// <summary>
        /// Send disconnect message to a connection with an optional reason for the disconnect
        /// </summary>
        /// <param name="connection">Connection</param>
        /// <param name="reason">Optional Reason for the disconnect</param>
        private void Disconnect (NetworkConnection connection, NetzDisconnectReason reason = NetzDisconnectReason.Unknown)
        {
            using var message = NetzMessage.Create(null, NetzConstants.Messages.Disconnect);
            var writer = message.BeginWrite();
            writer.WriteByte((byte)reason);
            message.EndWrite(writer);
            SendMessage(connection, message);

            connection.Disconnect(_driver);
        }

        /// <summary>
        /// Disconnect the given connected client
        /// </summary>
        /// <param name="client">Client to disconnect</param>
        /// <param name="reason"></param>
        private void Disconnect(ConnectedClient client, NetzDisconnectReason reason = NetzDisconnectReason.Unknown)
        {
            _clients.Remove(client);

            // Send a disconnect message as long as the client is still connected.
            if(_driver.GetConnectionState(client.connection) == NetworkConnection.State.Connected)
                Disconnect(client.connection, reason);
        }

        private void SendMessage (NetworkConnection c, NetzMessage message)
        {
            _driver.BeginSend(_pipeline, c, out var clientWriter);
            clientWriter.WriteBytes(message.buffer, message.length);
            _driver.EndSend(clientWriter);
        }

        private void UpdateClientStateConnecting (ConnectedClient client)
        {
            if (client.state != NetzClientState.Connecting)
                return;

            var elapsed = client.timeSinceLastMessageSent;
            if (elapsed >= NetzConstants.ConnectTimeout)
            {
                Disconnect(client, NetzDisconnectReason.Timeout);
                return;
            }

            // Resend connection messages every second until we get a response or timeout
            if (elapsed >= NetzConstants.ConnectMessageResendInterval)
            {
                // Send connect message to client
                using (var message = NetzMessage.Create(null, NetzConstants.Messages.Connect))
                {
                    var writer = message.BeginWrite();
                    writer.WriteUInt(client.id);
                    message.EndWrite(writer);

                    SendToClient(client, message);
                }
            }
        }

        private void UpdateClientStateConnected(ConnectedClient client)
        {
            if (state != NetzServerState.Active)
                return;

            SetClientState(client, NetzClientState.Synchronizing);
        }

        /// <summary>
        /// Accept incoming connections to the server
        /// </summary>
        private void AcceptConnections ()
        {
            // Accept new connections
            NetworkConnection c;
            while ((c = _driver.Accept()) != default(NetworkConnection))
            {
                // Reject the client if the maximum number of clients has been reached
                if(_clients.Count >= MaxClients)
                {
                    Disconnect(c, NetzDisconnectReason.ServerFull);
                    continue;
                }

                // TODO: handle reconnecting clients

                // Add a new client
                var time = Time.realtimeSinceStartupAsDouble;
                var client = new ConnectedClient { 
                    connection = c, 
                    state = NetzClientState.Connecting, 
                    id = _nextClientId++,
                    lastMessageReceiveTime = time,
                    lastMessageSendTime = time - NetzConstants.ConnectMessageResendInterval
                };

                _clients.Add(client);
            }
        }

        private void UpdateClient (ConnectedClient client)
        {
            // Read incoming data from all clients
            DataStreamReader stream;

            // Read all available events for the client
            NetworkEvent.Type cmd;
            while ((cmd = _driver.PopEventForConnection(client.connection, out stream)) != NetworkEvent.Type.Empty)
            {
                switch(cmd)
                {
                    case NetworkEvent.Type.Connect:
                        break;

                    case NetworkEvent.Type.Disconnect:
                        Disconnect(client);
                        break;

                    case NetworkEvent.Type.Data:
                        ReadMessage(client, stream);
                        break;
                }
            }

            switch (client.state)
            {
                case NetzClientState.Connecting:
                    UpdateClientStateConnecting(client);
                    break;

                case NetzClientState.Connected:
                    UpdateClientStateConnected(client);
                    break;

                case NetzClientState.Synchronizing:
                    UpdateClientStateSynchronizing(client);
                    break;

                case NetzClientState.Active:
                    UpdateClientStateActive(client);
                    break;
            }
        }

        private void UpdateClients ()
        {

        }

        internal void Update ()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (_debugger != null)
                _debugger.Update();
#endif

            _driver.ScheduleUpdate().Complete();

            AcceptConnections();

            for (int i = _clients.Count - 1; i >= 0; i--)
                UpdateClient(_clients[i]);

#if false
            // Only send snapshots when in the running state.
            if (_state != NetzServerState.Running)
                return;

            // Throttle snapshots
            _snapshotElapsed += Time.unscaledDeltaTime;
            if (_snapshotElapsed >= _snapshotInterval)
                _snapshotElapsed -= _snapshotInterval;
            else
                return;

            // Send any new snapshot for all dirty objects
            while(NetzObjectManager._dirtyObjects.Count > 0)
            {
                var netobj = NetzObjectManager._dirtyObjects.First.Value;
                var state = netobj.state;

                // Set the state back to spanwed which will remove it from the dirty objects list
                netobj.state = NetzObjectState.Spawned;

                // Do not need to send snapshots to ourself when we are the host, just skip if we are the only client
                // TODO: we should also skip this when there arent any clients that are fully connected yet.
                if (_clients.Count == 1 && _clients[0].player.isHost)
                    continue;

                // If the object is spawning we first need to send a spawn message before we send the snapshow
                if(state == NetzObjectState.Spawning)
                {
                    using (var message = NetzMessage.Create(null, NetzConstants.Messages.Spawn))
                    {
                        var writer = message.BeginWrite();
                        writer.WriteULong(netobj.prefabHash);
                        writer.WriteUInt(netobj.ownerClientId);
                        writer.WriteULong(netobj.networkInstanceId);
                        writer.WriteTransform(netobj.transform);
                        // TODO: parent
                        message.EndWrite(writer);

                        // TODO: add the snapshot to this spawn message instead of sending a separate message

                        SendToAllClients(message, includeHost: false);
                    }
                }
                
                if (state == NetzObjectState.Despawning)
                {
                    using (var message = NetzMessage.Create(netobj, NetzConstants.Messages.Despawn))
                        SendToAllClients(message, includeHost: false);

                    UnityEngine.Object.Destroy(netobj.gameObject);
                }
                else
                {
                    using (var message = NetzMessage.Create(netobj, NetzConstants.Messages.Snapshot))
                    {
                        var writer = message.BeginWrite();
                        netobj.WriteSnapshot(ref writer);
                        message.EndWrite(writer);

                        SendToAllClients(message, includeHost: false);
                    }
                }
            }
#endif
#if false
            // 

            // Send snapshots to any clients that are still synchronizing
            // TODO: we need to use cached synchronization state here so this isnt so taxing
            for (int i = 0; i < _clients.Count; i++)
            {
                var client = _clients[i];
                if (client.state != NetzClientState.Synchronizing)
                    continue;

                foreach (var kv in NetzObjectManager._objects)
                {
                    var netobj = kv.Value;

                    if(!netobj.isSceneObject && !netobj.isCustomObject)
                    {
                        using (var message = NetzMessage.Create(null, NetzConstants.Messages.Spawn))
                        {
                            var writer = message.BeginWrite();
                            writer.WriteULong(netobj.prefabHash);
                            writer.WriteUInt(netobj.ownerClientId);
                            writer.WriteULong(netobj.networkInstanceId);
                            writer.WriteTransform(netobj.transform);
                            // TODO: parent
                            message.EndWrite(writer);

                            SendToClient(client, message);
                        }
                    }

                    using (var message = NetzMessage.Create(netobj, NetzConstants.Messages.Snapshot))
                    {
                        var writer = message.BeginWrite();
                        netobj.WriteSnapshot(ref writer);
                        message.EndWrite(writer);

                        SendToClient(client, message);
                    }
                }

                SetClientState(client, NetzClientState.Connected);
            }
#endif

#if false
            // Update any client states
            SendClientStates();

            // Send disconnect messages to any 
            for (int i = 0; i < _clients.Count; i++)
            {
                if (_clients[i].state == NetzClientState.Disconnected)
                {
                    using (var message = NetzMessage.Create(null, NetzConstants.Messages.Disconnect))
                        SendToClient(_clients[i], message);

                    _clients[i].connection = default(NetworkConnection);
                }
            }
#endif
        }

        /// <summary>
        /// Synchronize a client to the current server state.  This is generally done when a client
        /// first connects but can be done again later if the client desyncs
        /// </summary>
        /// <param name="client">Client to synchronize</param>
        private void UpdateClientStateSynchronizing (ConnectedClient client)
        {
            if (state != NetzServerState.Active)
                return;

            if (client.state != NetzClientState.Synchronizing)
                return;

            // If already synchronizing then make sure do not do it too often
            if (client.timeSinceLastMessageSent < NetzConstants.SynchronizeInterval)
                return;

            using var message = NetzMessage.Create(null, NetzConstants.Messages.Synchronize);
            var writer = message.BeginWrite();
            writer.WriteFixedString32(_scene);

            // Send all players to the client
            writer.WriteByte((byte)_clients.Count);
            for(int i=0; i< _clients.Count; i++)
            {
                var connectedClient = _clients[i];
                writer.WriteUInt(client.id);
                connectedClient.player.Serialize(ref writer);
            }

            message.EndWrite(writer);

            // TODO: send deleted scene objects
            // TODO: send snapshots for all objects modified (generally all but could be some non-modified scene objects)

            SendToClient(client, message);
        }

        private void OnSynchronizeAck (FourCC messageType, ConnectedClient client, ref DataStreamReader reader)
        {
            // Client is now active in the world and can receive snapshots
            SetClientState(client, NetzClientState.Active);
        }

        private void UpdateClientStateActive (ConnectedClient client)
        {
            // Too soon?
            if (client.lastMessageSendTime < _snapshotInterval)
                return;

            using (var message = NetzMessage.Create(null, NetzConstants.Messages.Snapshot))
            {
                SendToClient(client, message);
            }
        }

        private void UpdateConnectedClient (ConnectedClient client)
        {
            switch(client.state)
            {
                case NetzClientState.LoadingScene:
                    // TODO: send a message to the client telling them to load the scene
                    // TODO: when we know the client has loaded the scene then we can start synchronizing
                    // TODO: snapshot to the client will just contain the map
                    break;
            }
        }

        private void SetClientState (ConnectedClient client, NetzClientState state)
        {
            if (client.state == state)
                return;

            client.oldState = client.state;
            client.state = state;
            _clientStateDirty = true;
        }

        private void SendToClient(ConnectedClient client, NetzMessage message)
        {
            if (!_driver.IsCreated)
                return;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            SendToDebugger(client, message.id, length:message.length, received: false);
#endif

            client.lastMessageSendTime = Time.realtimeSinceStartupAsDouble;

            _driver.BeginSend(_pipeline, client.connection, out var clientWriter);
            clientWriter.WriteBytes(message.buffer, message.length);
            _driver.EndSend(clientWriter);
        }

        internal void SendToAllClients(NetzMessage message, bool includeHost = true)
        {
            if (!_driver.IsCreated)
                return;

            foreach (var client in _clients)
            {
                if (client.state != NetzClientState.Connected)
                    continue;

                // Skip the host?
                if (!includeHost && client.player.isHost)
                    continue;

                SendToClient(client, message);
            }
        }

        private void ReadMessage(ConnectedClient client, DataStreamReader reader)
        {
            // Read the network object instance identifer
            var networkInstanceId = reader.ReadULong();

            // Read the message FourCC
            var messageId = new FourCC(reader.ReadUInt());

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            SendToDebugger(client, messageId, length: reader.Length, received: true);
#endif

            // No target, global messages
            if (networkInstanceId == 0)
            {
                _router.Route(messageId, client, ref reader);
                return;
            }

            if (NetzObjectManager.TryGetObject(networkInstanceId, out var obj))
            {
                obj.HandleMessage(messageId, ref reader);
            }
        }

        /// <summary>
        /// Handle client connection ack message
        /// </summary>
        private void OnClientConnectAck(FourCC messageType, ConnectedClient client, ref DataStreamReader reader)
        {
            var clientId = reader.ReadUInt();

            // Disconnect the client if they are sending garbage
            if(clientId != client.id)
            {
                Disconnect(client);
                return;
            }

            // Read the player information
            client.player = Activator.CreateInstance(_playerType) as NetzPlayer;
            client.player.Deserialize(ref reader);

            // Is this client the host?
            client.player.isHost = client.id == (NetzClient.instance?.id ?? uint.MaxValue);

            // Transition the client to the connected state
            SetClientState(client, NetzClientState.Connected);
        }

        /// <summary>
        /// Handle notfication from client that they are disconnecting.  This is the graceful way for a client
        /// to leave a server as it cleans up properly.  
        /// </summary>
        private void OnClientDisconnect(FourCC messageType, ConnectedClient client, ref DataStreamReader reader)
        {
            // Go straight to disconnected as disconnecting would not mean anything to the server.
            SetClientState(client, NetzClientState.Disconnected);
        }

        /// <summary>
        /// Send the client states of all clients to the given client or all clients if no client is specified.
        /// </summary>
        /// <param name="client">Optional client to send to</param>
        private void SendClientStates (ConnectedClient client=null, bool force=false)
        {
            if (!force && !_clientStateDirty)
                return;

            _clientStateDirty = false;

            // If not a host then the client state needs to be reported here
            if (!NetzManager.instance.isHost)
            {
                for (int clientIndex = 0; clientIndex < _clients.Count; clientIndex++)
                {
                    var clientOut = _clients[clientIndex];
                    if(clientOut.oldState != clientOut.state)
                    {
                        var oldState = clientOut.oldState;
                        clientOut.oldState = clientOut.state;
                        NetzManager.instance.RaiseClientStateChanged(clientOut.id, oldState, clientOut.state);
                    }
                }

                return;
            }

            using(var message = NetzMessage.Create(null, NetzConstants.Messages.ClientStates))
            {
                var writer = message.BeginWrite();
                writer.WriteByte((byte)_clients.Count);
                for (int clientIndex = 0; clientIndex < _clients.Count; clientIndex++)
                {
                    var clientOut = _clients[clientIndex];
                    writer.WriteUInt(clientOut.id);
                    writer.WriteByte((byte)clientOut.state);
                }
                message.EndWrite(writer);

                if (client == null)
                    SendToAllClients(message);
                else
                    SendToClient(client, message);
            }
        }

        public Coroutine LoadSceneAsync (string sceneName)
        {
            IEnumerator LoadCoroutine()
            {
                // Move all clients that are in some form of connected state back to connected.  This 
                // will ensure they do not receive any updates besides a keep alive until the server
                // is finished loading the scene.
                for (int i = 0; i < _clients.Count; i++)
                    if (_clients[i].state >= NetzClientState.Connected)
                        SetClientState(_clients[i], NetzClientState.Connected);

                // Unload the old scene
                if (_scene != null)
                {
                    yield return SceneManager.UnloadSceneAsync(_scene);

                    _scene = null;
                }

                // Send a message to all clients to load a new scene
                using (var message = NetzMessage.Create(null, NetzConstants.Messages.LoadScene))
                {
                    var writer = message.BeginWrite();
                    writer.WriteFixedString32(sceneName);
                    message.EndWrite(writer);

                    for (int clientIndex = 0; clientIndex < _clients.Count; clientIndex++)
                    {
                        var client = _clients[clientIndex];
                        if (client.player.isHost || client.state != NetzClientState.Connected)
                            continue;

                        client.state = NetzClientState.LoadingScene;

                        if (!client.player.isHost)
                            SendToClient(client, message);
                    }
                }

                // Load the scene and wait for it to finish to start snapshots back up
                yield return SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);

                _scene = sceneName;

                // Register and start all of the scene objects
                NetzObjectManager.SpawnSceneObjects();

                // Set state to running now that we have a loaded scene
                SetState(NetzServerState.Active);
            }

            // Switch to loading state until we are done loading scene
            SetState(NetzServerState.Loading);

            // Start a coroutine to load the state
            return NetzManager.instance.StartCoroutine(LoadCoroutine());
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private void SendToDebugger(ConnectedClient client, FourCC messageId, int length, bool received)
        {
            // Create the debugger if it is not there already
            if (_debugger == null)
            {
                var debuggerEndpoint = NetworkEndPoint.LoopbackIpv4;
                debuggerEndpoint.Port = 9191;
                _debugger = NetzDebuggerClient.Connect(debuggerEndpoint, 0);
            }

            _debugger.Send(from:0, to:client.id, messageId: messageId, received: received, length: length);
        }
#endif
    }
}

#endif