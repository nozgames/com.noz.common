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
        private double _lastMessageSendTime;
        private double _lastMessageReceiveTime;
        private ReliableEventQueueWriter _eventWriter;
        private ReliableEventQueueReader _eventReader;
        private Dictionary<uint, ConnectedPlayer> _connectedClients = new Dictionary<uint, ConnectedPlayer>();
        internal float _serverTimeDelta;

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
                var evt = _eventReader.Dequeue();
                var evtreader = evt.GetReader();

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

            var newDelta = Time.time - NetzTime.snapshotTime;
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
            NetzTime.snapshotTime = reader.ReadFloatDelta(NetzTime.lastSnapshotTime);

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

                // Find the objecct
                NetzObjectManager.TryGetObject(instanceId, out var netobj);

                netobj.Read(ref reader);
            }
        }

        private void OnConnectEvent (ReliableEvent evt)
        {
            var reader = evt.GetReader();
            id = reader.ReadUInt();

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

            // Raise player connected event for ourself
            onPlayerConnected?.Invoke(_player);
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
                NetzObjectManager.SpawnSceneObjects();

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

        private void OnObjectEvent (ReliableEvent evt)
        {
            if (!NetzObjectManager.TryGetObject(evt.target, out var netobj))
                return;

            var reader = evt.GetReader();
            netobj.ReadEvent(evt.tag, ref reader);
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
    }
}

#endif
