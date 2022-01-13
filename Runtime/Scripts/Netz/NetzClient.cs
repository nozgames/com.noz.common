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
        private NetworkConnection _connection;
        private NetworkDriver _driver;
        private NetworkPipeline _pipeline;
        private NetzPlayer _player;
        private string _scene;
        private double _lastMessageSendTime;
        private double _lastMessageReceiveTime;
        private ReliableEventQueueWriter _eventWriter;
        private ReliableEventQueueReader _eventReader;
        private Dictionary<uint, NetzPlayer> _players;
        internal float _serverTimeDelta;

        public static NetzClient instance { get; private set; }
        public static bool isCreated => instance != null;

        public NetworkConnection connection => _connection;

        /// <summary>
        /// True if the client is a host client
        /// </summary>
        public bool isHost { get; private set; }

        public static event Action<NetzPlayer> onPlayerConnected;
        public static event Action<NetzPlayer> onPlayerChanged;
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

        public float interpolation { get; private set; }

        /// <summary>
        /// Last snapshot number that was sent to the client
        /// </summary>
        internal int snapshotSent = 0;

        /// <summary>
        /// Last snapshot number that was received by the client
        /// </summary>
        internal int snapshotReceived = 0;

        internal uint _lastSnapshotReceived = 0;

        internal NetzClient(NetzPlayer player)
        {
            _player = player;
            _driver = NetworkDriver.Create(new FragmentationUtility.Parameters { PayloadCapacity = NetzConstants.MaxMessageSize });
            _pipeline = _driver.CreatePipeline(typeof(FragmentationPipelineStage));
            state = NetzClientState.Connecting;

            _eventWriter = new ReliableEventQueueWriter(NetzConstants.MaxReliableEvents, NetzConstants.ReliableEventBufferSize);
            _eventReader = new ReliableEventQueueReader(NetzConstants.MaxReliableEvents, NetzConstants.ReliableEventBufferSize);;

            _players = new Dictionary<uint, NetzPlayer>();
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
            // Issue a disconnect to the server
            if (_connection.IsCreated)
            {
                _driver.Disconnect(_connection);
                _driver.ScheduleUpdate().Complete();
                _connection = default(NetworkConnection);
            }

            if (_driver.IsCreated)
            {
                _driver.Dispose();
                _driver = default(NetworkDriver);
            }

            _eventReader.Dispose();
            _eventWriter.Dispose();

            instance = null;
        }

        private static int _dequeueLast = 0;

        internal void Update()
        {
            if (!_driver.IsCreated)
            {
                Disconnect();
                return;
            }

            _driver.ScheduleUpdate().Complete();

            if (!_connection.IsCreated)
            {
                Disconnect();
                return;
            }

            NetworkEvent.Type cmd;
            while ((cmd = connection.PopEvent(_driver, out var stream)) != NetworkEvent.Type.Empty)
            {
                switch (cmd)
                {
                    case NetworkEvent.Type.Connect:
                        state = NetzClientState.Connected;
                        break;

                    case NetworkEvent.Type.Data:
                        ReadSnapshot(new NetzReader(stream));
                        break;

                    case NetworkEvent.Type.Disconnect:
                        Disconnect();
                        return;
                }
            }

            if (state == NetzClientState.Connecting)
                return;

            // Process all incoming events
            while(_eventReader.count > 0)
            {
                if(_eventReader.Peek().sequenceId <= _dequeueLast)
                {
                    Debug.LogError("Double dequeue?");
                }

                var evt = _eventReader.Dequeue();
                var evtreader = evt.GetReader();

                _dequeueLast = (int)evt.sequenceId;

                if(evt.target != 0)
                {
                    OnObjectEvent(evt);
                    return;
                }

                switch(evt.tag)
                {
                    case NetzConstants.GlobalTag.Connect: OnConnectEvent(evt); break;
                    case NetzConstants.GlobalTag.LoadScene: OnLoadSceneEvent(evt); break;
                    case NetzConstants.GlobalTag.Synchronize: OnSynchronizeEvent(evt); break;
                    case NetzConstants.GlobalTag.PlayerInfo: OnPlayerInfoEvent(evt); break;
                    case NetzConstants.GlobalTag.PlayerDisconnect: OnPlayerDisconnectEvent(evt); break;
                    case NetzConstants.GlobalTag.Instantiate: OnInstantiateEvent(evt); break;
                    case NetzConstants.GlobalTag.Destroy: OnDestroyEvent(evt); break;
                }                
            }

            if (timeSinceLastMessageSent >= NetzManager.instance.clientUpdateInterval)
            {
                WriteSnapshot();
            }
        }

        private void WriteSnapshot()
        {
            _driver.BeginSend(_connection, out var writer);

            // Write the last processed incoming event so the client will stop sending it
            writer.WriteUInt(_eventReader.lastDequeuedSequenceId);

            // Write the outgoing events
            _eventWriter.Write(ref writer);

            _driver.EndSend(writer);

            _lastMessageSendTime = Time.realtimeSinceStartupAsDouble;
        }

        private void AdjustTimeDelta ()
        {
            // If this was the first snapshot then set the time delta
            if (NetzTime.lastSnapshotTime <= 0.0f)
            {
                _serverTimeDelta = NetzTime.snapshotTime - Time.time;
                return;
            }

            var newDelta = NetzTime.snapshotTime - Time.time;
            var deltaDelta = Mathf.Abs(newDelta - _serverTimeDelta);

            if (deltaDelta > 500)
                _serverTimeDelta = newDelta;
            else if (deltaDelta > 100)
                _serverTimeDelta = (newDelta + _serverTimeDelta) * 0.5f;
            else if (newDelta > _serverTimeDelta)
                _serverTimeDelta += 0.001f;
            else if (newDelta < _serverTimeDelta)
                _serverTimeDelta -= 0.001f;
        }

        private void ReadSnapshot(NetzReader reader)
        {
            // Update the global time.
            NetzTime.lastSnapshotTime = NetzTime.snapshotTime;
            NetzTime.snapshotTime = reader.ReadFloat();

            // Adjust the delta time 
            AdjustTimeDelta();

            // Remove all events from the outgoing queue that the server has seen
            var lastDequeuedOutgoingEventId = reader.ReadUInt();
            _eventWriter.Acknowledge(lastDequeuedOutgoingEventId);

            // Read all incoming events
            _eventReader.Read(ref reader._reader);
        }

        private void Synchronize (NetzReader reader)
        {
            var instanceId = 0UL;

            var scene = reader.ReadFixedString32();

            while(!reader.isEOF)
            {
                instanceId = reader.ReadULongDelta(instanceId);
                if (instanceId == 0)
                    break;

                var isSceneObject = reader.ReadBit();
                if (isSceneObject && NetzObject.TryGetTracked(instanceId, out var netobj))
                    netobj.Read(ref reader);
                else if (!isSceneObject)
                {
                    var prefabHash = reader.ReadULong();
                    if(NetzManager.instance.TryGetPrefab(prefabHash, out var prefab))
                    {
                        Instantiate(prefabHash, 0, instanceId, ref reader);
                    }
                }
            }
        }

        private void OnConnectEvent (ReliableEvent evt)
        {
            _players.Clear();

            var reader = evt.GetReader();
            id = reader.ReadUInt();

            while(!reader.isEOF)
            {
                var playerId = reader.ReadUInt();
                if (playerId == 0)
                    break;

                var player = Activator.CreateInstance(_player.GetType()) as NetzPlayer;
                player.id = playerId;
                player.isLocal = false;
                player.isHost = false;
                player.isConnected = true;
                player.Read(ref reader);

                _players[playerId] = player;
            }

            // Respond to the server with the player info
            var writer = _eventWriter.BeginEnqueue(0, NetzConstants.GlobalTag.Connect);
            writer.WriteUInt(id);
            _player.Write(ref writer);
            _eventWriter.EndEnqueue(writer);

            // Assume we are connected
            if(isHost)
                state = NetzClientState.Active;
            else
                state = NetzClientState.Synchronizing;

            // Raise player connected event for ourself and all other connected players
            onPlayerConnected?.Invoke(_player);

            foreach(var connectedPlayer in _players.Values)
                onPlayerConnected?.Invoke(connectedPlayer);
        }

        private void OnLoadSceneEvent (ReliableEvent evt)
        {
            // TODO: handle reloading of scene mid game

            IEnumerator LoadCoroutine(string sceneName)
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
                InstantiateSceneObjects();

                // Inform the server that we have finished loading the scene
                _eventWriter.Enqueue(0, NetzConstants.GlobalTag.LoadScene);

                // Move to the synchronized state to wait for synchronization
                state = NetzClientState.Synchronizing;
            }

            // Start a coroutine to load the state
            var sceneName = evt.GetReader().ReadFixedString32();
            NetzManager.instance.StartCoroutine(LoadCoroutine(sceneName));
        }

        private void OnSynchronizeEvent (ReliableEvent evt)
        {
            // Must be in synchronizing state
            if (state != NetzClientState.Synchronizing)
                return;

            Synchronize(evt.GetReader());

            state = NetzClientState.Active;
        }

        private void OnPlayerInfoEvent (ReliableEvent evt)
        {
            var reader = evt.GetReader();
            var playerId = reader.ReadUInt();
            var newPlayer = !_players.TryGetValue(playerId, out var player);
            if (newPlayer)
            {
                player = Activator.CreateInstance(_player.GetType()) as NetzPlayer;
                _players[playerId] = player;
            }

            player.Read(ref reader);

            if (newPlayer)
                onPlayerConnected?.Invoke(player);
            else
                onPlayerChanged?.Invoke(player);
        }

        private void OnPlayerDisconnectEvent (ReliableEvent evt)
        {
            var reader = evt.GetReader();
            var playerId = reader.ReadUInt();

            if (!_players.TryGetValue(playerId, out var player))
                return;

            _players.Remove(playerId);

            onPlayerDisconnected?.Invoke(player);
        }

        private void OnInstantiateEvent (ReliableEvent evt)
        {
            var reader = evt.GetReader();
            var networkId = reader.ReadULong();
            var prefabHash = reader.ReadULong();
            var playerId = reader.ReadUInt();
            Instantiate(prefabHash, playerId, networkId, ref reader);
        }

        private void OnDestroyEvent(ReliableEvent evt)
        {
            var reader = evt.GetReader();
            var networkId = reader.ReadULong();
            Destroy(networkId);
        }

        private void OnObjectEvent (ReliableEvent evt)
        {
            if (!NetzObject.TryGetTracked(evt.target, out var netzobj))
                return;

            var reader = evt.GetReader();
            netzobj.ReadEvent(evt.tag, ref reader);
        }

        /// <summary>
        /// Instantiate a network object on the client
        /// </summary>
        /// <param name="prefabHash">Prefab hash</param>
        /// <param name="ownerId">Identifier of the player that will own the object</param>
        /// <param name="networkInstanceId">Identifier of the object on the server</param>
        /// <param name="reader">Reader used to serialize in the objects state</param>
        /// <returns>Instantiated object</returns>
        private NetzObject Instantiate (ulong prefabHash, uint ownerId, ulong networkInstanceId, ref NetzReader reader)
        {
            // Look up the prefab
            if (!NetzManager.instance.TryGetPrefab(prefabHash, out var prefab))
            {
                Debug.LogError($"Unknown prefab hash `{prefabHash}`.  Make sure the prefab is included in the NetzManager prefab list.");
                return null;
            }

            // TODO: parent
            var netzobj = UnityEngine.Object.Instantiate(prefab.gameObject).GetComponent<NetzObject>();
            netzobj._networkInstanceId = networkInstanceId;
            netzobj.ownerId = ownerId;

            NetzObject.Track(netzobj);

            netzobj.Read(ref reader);

            netzobj.NetworkStart();

            return netzobj;
        }

        /// <summary>
        /// Instantiate all objects in the scene.  This is done after the scene is loaded 
        /// </summary>
        private void InstantiateSceneObjects ()
        {
            var sceneObjects = UnityEngine.Object.FindObjectsOfType<NetzObject>();
            foreach (var netzobj in sceneObjects)
                NetzObject.Track(netzobj);

            foreach (var netzobj in sceneObjects)
                netzobj.NetworkStart();
        }

        /// <summary>
        /// Destroy the network object specified by its network instance id
        /// </summary>
        /// <param name="networkInstanceId">Network object isntance id</param>
        internal void Destroy (ulong networkInstanceId)
        {
            if (NetzServer.isCreated)
                throw new InvalidOperationException("Network objects cannot be destroyed on the client when the server is running");

            if (!NetzObject.TryGetTracked(networkInstanceId, out var netzobj))
                return;

            NetzObject.Untrack(netzobj);

            netzobj.OnDespawn();

            UnityEngine.Object.Destroy(netzobj.gameObject);
        }

        internal NetzWriter BeginSendEvent(NetzObject target, ushort tag) =>
            _eventWriter.BeginEnqueue(target._networkInstanceId, tag);

        internal void EndSendEvent(NetzWriter writer) =>
            _eventWriter.EndEnqueue(writer);

    }
}

#endif
