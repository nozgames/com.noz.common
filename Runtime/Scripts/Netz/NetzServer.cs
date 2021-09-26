#if UNITY_COLLECTIONS && UNITY_TRANSPORT

using System;
using System.Collections.Generic;
using System.Diagnostics;
using Unity.Collections;
using Unity.Networking.Transport;
using Unity.Networking.Transport.Utilities;
using UnityEngine;
using UnityEngine.Assertions;

namespace NoZ.Netz
{
    internal unsafe class NetzServer : IDisposable
    {
        private class ConnectedClient
        {
            public NetworkConnection connection;
            public NetzClientState oldState;
            public NetzClientState state;
            public int lastSnapshotSent;
            public int lastSnapshotReceived;
            public uint id;
            public bool isHost;
        }

        private NetworkDriver _driver;
        private NetworkPipeline _pipeline;
        private List<ConnectedClient> _clients;
        private uint _nextClientId = 1;
        private NetzMessageRouter<ConnectedClient> _router;
        private float _snapshotInterval = 1.0f / 20.0f;
        private float _snapshotElapsed = 0.0f;

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

        /// <summary>
        /// Event raised when a clients state changes
        /// </summary>
        public event ClientStateChangeEvent onClientStateChanged;

        /// <summary>
        /// Number of clients connected to the server
        /// </summary>
        public int clientCount => _clients.Count;

        private NetzServer()
        {
            _clients = new List<ConnectedClient>(16);
            _driver = NetworkDriver.Create(new ReliableUtility.Parameters { WindowSize = 32 });
            _pipeline = _driver.CreatePipeline(typeof(ReliableSequencedPipelineStage));

            _router.AddRoute(NetzConstants.Messages.Connect, OnClientConnectAck);
            _router.AddRoute(NetzConstants.Messages.Disconnect, OnClientDisconnect);
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

            if (_driver.IsCreated)
            {
                foreach (var client in _clients)
                    client.connection.Close(_driver);

                _driver.Dispose();
            }
        }

        public static NetzServer Create (NetworkEndPoint endpoint)
        {
            var server = new NetzServer();

            if (server._driver.Bind(endpoint) != 0)
            {
                server.Dispose();
                return null;
            }
            else
                server._driver.Listen();

            server._snapshotInterval = 1.0f / NetzManager.instance._updateRate;

            return server;
        }

        internal void Update ()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (_debugger != null)
                _debugger.Update();
#endif

            _driver.ScheduleUpdate().Complete();

            // Clean up any clients that have disconnected
            for (int i = 0; i < _clients.Count; i++)
            {
                if (!_clients[i].connection.IsCreated)
                {
                    _clients.RemoveAtSwapBack(i);
                    --i;
                }
            }

            // Accept new connections
            NetworkConnection c;
            while ((c = _driver.Accept()) != default(NetworkConnection))
            {
                var client = new ConnectedClient { connection = c, state = NetzClientState.Connecting, id = _nextClientId++ };
                _clients.Add(client);

                // Send a connect message to the client to assign it an identifier
                using (var message = NetzMessage.Create(null, NetzConstants.Messages.Connect))
                {
                    var writer = message.BeginWrite();
                    writer.WriteUInt(client.id);
                    message.EndWrite(writer);

                    SendToClient(client, message);
                }
            }

            // Read incoming data from all clients
            DataStreamReader stream;
            for (int i = 0; i < _clients.Count; i++)
            {
                var client = _clients[i];
                Assert.IsTrue(client.connection.IsCreated);

                NetworkEvent.Type cmd;
                while ((cmd = _driver.PopEventForConnection(client.connection, out stream)) != NetworkEvent.Type.Empty)
                {
                    if (cmd == NetworkEvent.Type.Data)
                    {
                        ReadMessage(client, stream);
                    }
                    else if (cmd == NetworkEvent.Type.Disconnect)
                    {
                        _clients.RemoveAtSwapBack(i);
                        i--;
                    }
                }
            }

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
                if (_clients.Count == 1 && _clients[0].isHost)
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

#if true
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
                if (!includeHost && NetzManager.instance.localClientId == client.id)
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
            UnityEngine.Debug.Assert(client.id == clientId);

            // Is this client the host?
            client.isHost = client.id == NetzManager.instance.localClientId && NetzManager.instance.isServer;

            SetClientState(client, client.isHost ? NetzClientState.Connected : NetzClientState.Synchronizing);
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