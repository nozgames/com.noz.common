#if UNITY_COLLECTIONS && UNITY_TRANSPORT

using UnityEngine;
using Unity.Networking.Transport;
using System;
using System.Collections.Generic;

namespace NoZ.Netz
{
    internal unsafe class NetzClient : IDisposable
    {
        private const float KeepAliveDuration = 10.0f;

        private class ConnectedClient
        {
            public uint id;
            public NetzClientState oldState;
            public NetzClientState state;
        }

        private NetworkConnection _connection;
        private NetworkDriver _driver;
        private NetworkPipeline _reliablePipeline;
        private float _nextKeepAlive;
        private NetzMessageRouter<NetzClient> _router;
        private Dictionary<uint, ConnectedClient> _connectedClients = new Dictionary<uint, ConnectedClient>();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private NetzDebuggerClient _debugger;
#endif

        public NetworkConnection connection => _connection;


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


        private NetzClient ()
        {
            _driver = NetworkDriver.Create();
            _reliablePipeline = _driver.CreatePipeline(typeof(ReliableSequencedPipelineStage));
            _nextKeepAlive = KeepAliveDuration;
            state = NetzClientState.Connecting;

            _router = new NetzMessageRouter<NetzClient>();
            _router.AddRoute(NetzConstants.Messages.Connect, OnConnectMessage);
            _router.AddRoute(NetzConstants.Messages.Spawn, OnSpawnMessage);
            _router.AddRoute(NetzConstants.Messages.ClientStates, OnClientStates);
        }

        public static NetzClient Connect (NetworkEndPoint endpoint)
        {
            var client = new NetzClient ();
            client._connection = client._driver.Connect(endpoint);
            return client;
        }

        internal void Update ()
        {
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
                switch(cmd)
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
                        _connection = default(NetworkConnection);
                        break;
                }
            }
        }

        private void ReadMessage(DataStreamReader reader)
        {
            // Read the network object instance identifer
            var networkInstanceId = reader.ReadULong();

            // Read the message FourCC
            var messageId = new FourCC(reader.ReadUInt());

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // Ignore connect message for debugger becuase it will send to the debugger itself
            if(messageId != NetzConstants.Messages.Connect)
                SendToDebugger(messageId, received: true, length: reader.Length);
#endif

            // Global messages go through the router
            if (networkInstanceId == 0)
                _router.Route(messageId, this, ref reader);
            // Messages for objects get routed to the object
            else if (NetzObjectManager.TryGetObject(networkInstanceId, out var obj))
                obj.HandleMessage(messageId, ref reader);
        }

        public void Dispose()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (_debugger != null)
            {
                _debugger.Dispose();
                _debugger = null;
            }
#endif

            if (_connection.IsCreated)
                _connection.Close(_driver);

            if(_driver.IsCreated)
                _driver.Dispose();
        }

        private void OnConnectMessage (FourCC messageId, NetzClient client, ref DataStreamReader reader)
        {
            id = reader.ReadUInt();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // We have to special case connect message for the debugger because we want the client
            // identifier to be filled in before we send to the debugger.
            SendToDebugger(messageId, received: true, length: reader.Length);
#endif

            using (var message = NetzMessage.Create(null, NetzConstants.Messages.Connect))
            {
                var writer = message.BeginWrite();
                writer.WriteUInt(id);
                message.EndWrite(writer);

                SendToServer(message);
            }
        }

        /// <summary>
        /// Handles incoming object spawn messages
        /// </summary>
        private void OnSpawnMessage(FourCC messageId, NetzClient target, ref DataStreamReader reader)
        {
            var prefabHash = reader.ReadULong();
            var networkInstanceId = reader.ReadULong();
            var netzObject = NetzObjectManager.SpawnOnClient(prefabHash, networkInstanceId);
            if (null == netzObject)
                return;

            reader.ReadTransform(netzObject.transform);
        }

        /// <summary>
        /// Handles incoming client state changes
        /// </summary>
        private void OnClientStates(FourCC messageId, NetzClient target, ref DataStreamReader reader)
        {
            // First mark all connected clients as disconnected assuming their state will be updated
            // in the loop below
            foreach(var connectedClient in _connectedClients.Values)
                connectedClient.state = NetzClientState.Disconnected;

            var count = (int)reader.ReadByte();
            for(var clientIndex=0; clientIndex<count; clientIndex++)
            {
                var clientId = reader.ReadUInt();
                var state = (NetzClientState)reader.ReadByte();

                if (!_connectedClients.TryGetValue(clientId, out var connectedClient))
                    _connectedClients.Add(clientId, new ConnectedClient { id = clientId, oldState = NetzClientState.Unknown, state = state });
                else
                    connectedClient.state = state;
            }

            // Send events for any state changes
            foreach (var connectedClient in _connectedClients.Values)
                if(connectedClient.oldState != connectedClient.state)
                {
                    var oldState = connectedClient.oldState;
                    connectedClient.oldState = connectedClient.state;
                    NetzManager.instance.RaiseClientStateChanged(connectedClient.id, oldState, connectedClient.state);                    
                }
        }

        /// <summary>
        /// Send a keep alive message to the server to ensure that the connection isnt closed.
        /// </summary>
        private void SendKeepAlive ()
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

            _driver.BeginSend(_reliablePipeline, connection, out var clientWriter);
            clientWriter.WriteBytes(message.buffer, message.length);
            _driver.EndSend(clientWriter);

            // Since we sent a message we can reset the keep alive
            _nextKeepAlive = KeepAliveDuration;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private void SendToDebugger (FourCC messageId, int length, bool received)
        {
            // Create the debugger if it is not there already
            if (_debugger == null)
            {
                var debuggerEndpoint = NetworkEndPoint.LoopbackIpv4;
                debuggerEndpoint.Port = 9191;
                _debugger = NetzDebuggerClient.Connect(debuggerEndpoint, id);
            }

            _debugger.Send(from:id, to:0, messageId, received: received, length:length);
        }
#endif
    }
}

#endif
