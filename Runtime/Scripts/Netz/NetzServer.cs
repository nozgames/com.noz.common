#if UNITY_COLLECTIONS && UNITY_TRANSPORT

using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Networking.Transport;
using Unity.Networking.Transport.Utilities;
using UnityEngine;
using UnityEngine.SceneManagement;

// TODO: every frame we need to find the minimum ack'ed snapshot from all clients. 
// TODO: discard any old snapshot events
// TODO: despawning an object is an event that will exist in the event queue until all clients have reconigzed it

namespace NoZ.Netz
{
    [DefaultExecutionOrder(int.MaxValue)]
    public unsafe class NetzServer
    {
        private class ConnectedClient
        {
            public NetworkConnection connection;
            public NetzClientState oldState;
            public NetzClientState state;
            public uint lastAcknowledgedSnapshopId;
            public uint lastSendSnapshotId;
            public uint id;
            public double lastMessageSendTime;
            public double lastMessageReceiveTime;
            public NetzPlayer player;
            public ReliableEventQueueWriter eventWriter;
            public ReliableEventQueueReader eventReader;
            public float lastSnapshotTime;

            public double timeSinceLastMessageSent => Time.realtimeSinceStartupAsDouble - lastMessageSendTime;
            public double timeSinceLastMessageReceived => Time.realtimeSinceStartupAsDouble - lastMessageReceiveTime;
        }

        public static event Action<NetzPlayer> onPlayerConnected;
        public static event Action<NetzPlayer> onPlayerDisconnected;

        private NetworkEndPoint _endpoint;
        private NetworkDriver _driver;
        private NetworkPipeline _pipeline;
        private List<ConnectedClient> _clients;
        private uint _nextClientId = 1;
        private float _snapshotInterval = 1.0f / 20.0f;
        private string _scene;
        private Type _playerType;
        private NetzServerState _state = NetzServerState.Unknown;
        private ReliableEventQueueWriter _events;
        private ulong _nextSpawnedObjectInstanceId = NetzConstants.SpawnedObjectInstanceId;

        /// <summary>
        /// Identifier of the current snapshot
        /// </summary>
        private uint _snapshotId = 1;

        /// <summary>
        /// Singleton instance
        /// </summary>
        public static NetzServer instance { get; private set; }

        /// <summary>
        /// True if the server has been created
        /// </summary>
        public static bool isCreated => instance != null;

        public NetzServerState state => _state;

        public int MaxClients { get; set; } = 16;

        public ushort port => _endpoint.Port;

        /// <summary>
        /// Number of players connected to the server
        /// </summary>
        public int playerCount => _clients.Count;

        public uint currentSnapshotId => _snapshotId;

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

            _endpoint = NetworkEndPoint.LoopbackIpv4;
            _endpoint.Port = port;
            _snapshotInterval = 1.0f / NetzManager.instance._updateRate;

            _events = new ReliableEventQueueWriter(NetzConstants.MaxReliableEvents, NetzConstants.ReliableEventBufferSize);

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
            if (_driver.IsCreated)
            {
                foreach (var client in _clients)
                {
                    client.connection.Disconnect(_driver);
                    client.eventWriter.Dispose();
                    client.eventReader.Dispose();
                }

                _driver.Dispose();
            }

            _events.Dispose();

            instance = null;
        }

        private void SetState (NetzServerState state)
        {
            var old = _state;
            _state = state;
            NetzManager.instance.RaiseServerStateChanged(old, state);
        }

        /// <summary>
        /// Return the player at the given index
        /// </summary>
        /// <param name="index">Index of the player</param>
        /// <returns>Player</returns>
        public NetzPlayer GetPlayerAt (int index) => _clients[index].player;

        /// <summary>
        /// Return the player at the given index
        /// </summary>
        /// <typeparam name="T">Player Info Type</typeparam>
        /// <param name="index">Index of the player</param>
        /// <returns>Player</returns>
        public T GetPlayerAt<T>(int index) where T : NetzPlayer => _clients[index].player as T;

        public NetzClientState GetClientState(int index) => _clients[index].state;


        /// <summary>
        /// Send disconnect message to a connection with an optional reason for the disconnect
        /// </summary>
        /// <param name="connection">Connection</param>
        /// <param name="reason">Optional Reason for the disconnect</param>
        private void Disconnect (NetworkConnection connection, NetzDisconnectReason reason = NetzDisconnectReason.Unknown)
        {
            connection.Disconnect(_driver);
        }

        /// <summary>
        /// Disconnect the given connected client
        /// </summary>
        /// <param name="client">Client to disconnect</param>
        /// <param name="reason"></param>
        private void Disconnect(ConnectedClient client, NetzDisconnectReason reason = NetzDisconnectReason.Unknown)
        {
            // Send a disconnect to all other clients
            SendPlayerDisconnectEvent(client);

            _clients.Remove(client);

            // Send a disconnect message as long as the client is still connected.
            if(_driver.GetConnectionState(client.connection) == NetworkConnection.State.Connected)
                Disconnect(client.connection, reason);

            client.eventWriter.Dispose();
            client.eventReader.Dispose();

            onPlayerDisconnected?.Invoke(client.player);
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
                    lastMessageSendTime = time - NetzConstants.ConnectMessageResendInterval,
                    eventWriter = new ReliableEventQueueWriter(NetzConstants.MaxReliableEvents, NetzConstants.ReliableEventBufferSize),
                    eventReader = new ReliableEventQueueReader(NetzConstants.MaxReliableEvents, NetzConstants.ReliableEventBufferSize)
                };

                // Send the connect event to the client to assign it an identifier
                var writer = client.eventWriter.BeginEnqueue(0, NetzConstants.GlobalTag.Connect);
                writer.WriteUInt(client.id);

                // Write all known clients
                for(int i=0; i < _clients.Count; i++)
                {
                    if (_clients[i].state == NetzClientState.Connecting)
                        continue;

                    writer.WriteUInt(_clients[i].id);
                    _clients[i].player.Write(ref writer);
                }
                writer.WriteUInt(0);

                client.eventWriter.EndEnqueue(writer);

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
                        return;

                    case NetworkEvent.Type.Data:
                        ReadSnapshot (client, stream);
                        break;
                }
            }

            // Process incoming events
            while (client.eventReader.count > 0)
            {
                var evt = client.eventReader.Dequeue();

                // Global tag?
                if (evt.target == 0)
                {
                    switch(evt.tag)
                    {
                        case NetzConstants.GlobalTag.Connect: OnConnectEvent(client, evt); break;
                        case NetzConstants.GlobalTag.LoadScene: OnLoadSceneEvent(client, evt); break;
                    }
                }
                else
                    OnObjectEvent(client, evt);
            }


            // Send snapshot to the client 
//            if(client.timeSinceLastMessageSent >= _snapshotInterval)
//            {
                WriteSnapshot(client);
  //          }
        }

        private void SendLoadSceneEvent (ConnectedClient client)
        {
            var writer = client.eventWriter.BeginEnqueue(0, NetzConstants.GlobalTag.LoadScene);
            writer.WriteFixedString32(_scene);
            client.eventWriter.EndEnqueue(writer);
        }

        private void SendSynchronizeEvent (ConnectedClient client)
        {
            // TODO: write deleted scene objects

            var writer = client.eventWriter.BeginEnqueue(0, NetzConstants.GlobalTag.Synchronize);
            writer.WriteFixedString32(_scene);

            var baseInstanceId = 0UL;
            for(var node = NetzObject._objects.First; node != null; node = node.Next)
            {
                var netobj = node.Value;
                writer.WriteULongDelta(netobj._networkInstanceId, baseInstanceId);
                writer.WriteBit(netobj.isSceneObject);

                if(!netobj.isSceneObject)
                    writer.WriteULong(netobj.prefabHash);

                netobj.Write(ref writer);
                baseInstanceId = netobj._networkInstanceId;
            }

            writer.WriteULongDelta(0, baseInstanceId);

            client.eventWriter.EndEnqueue(writer);
        }

        internal void SentInstantiateEvent (NetzObject netobj)
        {
            foreach(var client in _clients)
            {
                // Do not send spawn events to the host, the object is already spawned
                if (client.player.isHost)
                    continue;

                // Only active clients need spawn events because the synchronize event will
                // issue spawn events for all objects as well
                if (client.state != NetzClientState.Active)
                    continue;

                var writer = client.eventWriter.BeginEnqueue(0, NetzConstants.GlobalTag.Instantiate);
                writer.WriteULong(netobj._networkInstanceId);
                writer.WriteULong(netobj.prefabHash);
                writer.WriteUInt(netobj.ownerId);
                netobj.Write(ref writer);
                client.eventWriter.EndEnqueue(writer);
            }
        }

        internal void SendDestroyEvent (NetzObject netobj)
        {
            foreach (var client in _clients)
            {
                // Do not send spawn events to the host, the object is already spawned
                if (client.player.isHost)
                    continue;

                if (client.state != NetzClientState.Active)
                    continue;

                var writer = client.eventWriter.BeginEnqueue(0, NetzConstants.GlobalTag.Destroy);
                writer.WriteULong(netobj._networkInstanceId);
                client.eventWriter.EndEnqueue(writer);
            }
        }

        internal void Update ()
        {
            _driver.ScheduleUpdate().Complete();

            AcceptConnections();

            // All events that are generated during a server frame are added to a central 
            // event queue which must now be appended to all connected clients.
            if(_events.count > 0)
            {
                for (int i = _clients.Count - 1; i >= 0; i--)
                {
                    var client = _clients[i];
                    // TODO: should specific events have flags so we know which to send to the server?
                    if(client.state == NetzClientState.Active && !client.player.isHost)
                        client.eventWriter.Enqueue(_events);
                }

                _events.Clear();
            }

            for (int i = _clients.Count - 1; i >= 0; i--)
                UpdateClient(_clients[i]);
        }

        private void SetClientState (ConnectedClient client, NetzClientState state)
        {
            if (client.state == state)
                return;

            client.oldState = client.state;
            client.state = state;
        }

        /// <summary>
        /// Read an incoming scnapshot from the client
        /// </summary>
        /// <param name="client"></param>
        /// <param name="reader"></param>
        private void ReadSnapshot (ConnectedClient client, DataStreamReader reader)
        {
            // Remove outgoing events that the client has processed
            var lastDequeuedOutgoingEventId = reader.ReadUInt();
            client.eventWriter.Acknowledge(lastDequeuedOutgoingEventId);

            // Read all incoming events
            client.eventReader.Read(ref reader);
        }

        private NetzWriter BeginSend (ConnectedClient client)
        {
            _driver.BeginSend(client.connection, out var writer);
            return new NetzWriter(writer);
        }

        private void EndSend(NetzWriter writer) =>
            _driver.EndSend(writer._writer);


        /// <summary>
        /// Write a new snapshot to the client
        /// </summary>
        /// <param name="client"></param>
        private void WriteSnapshot (ConnectedClient client)
        {
            var writer = BeginSend(client);
            writer.WriteFloat(Time.time);
            client.lastSnapshotTime = Time.time;

            // Write the last processed incoming event so the client will stop sending it
            writer.WriteUInt(client.eventReader.lastDequeuedSequenceId);

            // Write the outgoing events
            client.eventWriter.Write(ref writer._writer);

            EndSend(writer);

            client.lastMessageSendTime = Time.realtimeSinceStartupAsDouble;
        }

        /// <summary>
        /// Handle connect event from client
        /// </summary>
        private void OnConnectEvent (ConnectedClient client, ReliableEvent evt)
        {
            // Connect event should only be received while waiting for connection.
            if (client.state != NetzClientState.Connecting)
                return;

            // Drop any events on the floor that are for a target while connecting
            if (evt.target != 0)
                return;

            // Skip any events that are not Connect, they shouldnt be there.
            if (evt.tag != NetzConstants.GlobalTag.Connect)
                return;

            var reader = evt.GetReader();
            var clientId = reader.ReadUInt();

            // Disconnect the client if they are sending garbage
            if(clientId != client.id)
            {
                Disconnect(client);
                return;
            }

            // Read the player information
            client.player = Activator.CreateInstance(_playerType) as NetzPlayer;
            client.player.Read(ref reader);
            client.player.id = client.id;

            // Is this client the host?
            client.player.isHost = client.id == (NetzClient.instance?.id ?? uint.MaxValue);

            SendPlayerInfoEvent(client);

            if (!client.player.isHost)
            {
                SendLoadSceneEvent(client);
                SetClientState(client, NetzClientState.LoadingScene);
            }
            else
                SetClientState(client, NetzClientState.Active);

            onPlayerConnected?.Invoke(client.player);
        }

        /// <summary>
        /// Send a player info event to all other connected clients
        /// </summary>
        private void SendPlayerInfoEvent(ConnectedClient client)
        {
            foreach (var connectedClient in _clients)
            {
                if (connectedClient.state == NetzClientState.Connecting || connectedClient == client)
                    continue;

                var writer = connectedClient.eventWriter.BeginEnqueue(0, NetzConstants.GlobalTag.PlayerInfo);
                writer.WriteUInt(connectedClient.id);
                connectedClient.player.Write(ref writer);
                connectedClient.eventWriter.EndEnqueue(writer);
            }
        }

        /// <summary>
        /// Send a player disconnect message to all other connected players
        /// </summary>
        /// <param name="client"></param>
        private void SendPlayerDisconnectEvent (ConnectedClient client)
        {
            foreach (var connectedClient in _clients)
            {
                if (connectedClient.state == NetzClientState.Connecting || connectedClient == client)
                    continue;

                var writer = connectedClient.eventWriter.BeginEnqueue(0, NetzConstants.GlobalTag.PlayerDisconnect);
                writer.WriteUInt(connectedClient.id);
                connectedClient.eventWriter.EndEnqueue(writer);
            }
        }

        /// <summary>
        /// Event sent from client when loading of a scene is finished. 
        /// </summary>
        private void OnLoadSceneEvent (ConnectedClient client, ReliableEvent evt)
        {
            // This event should only be sent when waiting for the client to load a scene
            if (client.state != NetzClientState.LoadingScene)
                return;

            SendSynchronizeEvent(client);

            // Now that a synchronization event has been sent we can assume the client will 
            // be active once the synchronize is finished so we can start sending updates
            SetClientState(client, NetzClientState.Active);
        }

        /// <summary>
        /// Handle an incoming object event from a client 
        /// </summary>
        private void OnObjectEvent (ConnectedClient client, ReliableEvent evt)
        {
            // Client should not be sneding object events unless they are active
            if (client.state != NetzClientState.Active)
                return;

            // TODO: permissions
            // TODO: route the mesage to the object
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

                // Load the scene and wait for it to finish to start snapshots back up
                var ao = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
                ao.allowSceneActivation = true;
                yield return ao;

                _scene = sceneName;

                // Register and start all of the scene objects
                InstantiateSceneObject();

                // Set state to running now that we have a loaded scene
                SetState(NetzServerState.Active);
            }

            // Switch to loading state until we are done loading scene
            SetState(NetzServerState.Loading);

            // Start a coroutine to load the state
            return NetzManager.instance.StartCoroutine(LoadCoroutine());
        }

        internal NetzWriter BeginSendEvent (NetzObject target, ushort tag) =>
            _events.BeginEnqueue(target._networkInstanceId, tag);

        internal void EndSendEvent (NetzWriter writer) =>
            _events.EndEnqueue(writer);


        /// <summary>
        /// Instantiate a new network object on the server
        /// </summary>
        /// <param name="ownerId">Player that ownes the object </param>
        /// <param name="prefab">Prefab</param>
        /// <param name="parent">Optional parent object</param>
        /// <returns>Instantiated network object</returns>
        public NetzObject Instantiate(uint ownerId, NetzObject prefab, NetzObject parent = null) =>
            Instantiate(ownerId, prefab, parent, Vector3.zero, Quaternion.identity);

        /// <summary>
        /// Instantiate a new network object on the server
        /// </summary>
        /// <param name="ownerId">Player that ownes the object </param>
        /// <param name="prefab">Prefab</param>
        /// <param name="parent">Optional parent object</param>
        /// <param name="position">Starting position</param>
        /// <param name="rotation">Starting rotation</param>
        /// <returns>Instantiated network object</returns>
        public NetzObject Instantiate(uint ownerId, NetzObject prefab, NetzObject parent, Vector3 position, Quaternion rotation)
        {
            // Instantiate the actual game object
            var go = UnityEngine.Object.Instantiate(prefab.gameObject, parent == null ? null : parent.transform);
            var netzobj = go.GetComponent<NetzObject>();
            if (null == netzobj)
            {
                UnityEngine.Object.Destroy(go);
                return null;
            }

            netzobj._networkInstanceId = _nextSpawnedObjectInstanceId++;
            netzobj.prefabHash = prefab.prefabHash;
            netzobj.ownerId = ownerId;
            netzobj.transform.position = position;
            netzobj.transform.rotation = rotation;

            // Track the object
            NetzObject.Track(netzobj);

            netzobj.NetworkStart();

            SentInstantiateEvent(netzobj);

            return netzobj;
        }

        /// <summary>
        /// Instantiate all objects within the scene.
        /// </summary>
        private void InstantiateSceneObject()
        {
            // Track all scene objects
            var sceneObjects = UnityEngine.Object.FindObjectsOfType<NetzObject>();
            foreach (var netobj in sceneObjects)
                NetzObject.Track(netobj);

            // Issue a network start on all scene objects
            foreach (var obj in sceneObjects)
                obj.NetworkStart();
        }

        /// <summary>
        /// Despawn a networked object
        /// </summary>
        /// <param name="netzobj">Object to destroy</param>
        public void Destroy(NetzObject netzobj)
        {
            NetzObject.Untrack(netzobj);

            netzobj.OnDespawn();

            // TODO: if this is a scene object we need to track the destroy

            SendDestroyEvent(netzobj);

            // Destroy the game object
            UnityEngine.Object.Destroy(netzobj.gameObject);
        }
    }
}

#endif