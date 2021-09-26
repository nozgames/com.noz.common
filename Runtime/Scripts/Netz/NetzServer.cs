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
            public NetzClientState state;
            public int lastSnapshotSent;
            public int lastSnapshotReceived;
            public uint id;
            public bool isHost;
        }

        private NetworkDriver _driver;
        private NetworkPipeline _reliablePipeline;
        private List<ConnectedClient> _clients;
        private uint _nextClientId = 1;
        private NetzMessageRouter<ConnectedClient> _router;
        private float _snapshotInterval = 60.0f / 20.0f;
        private float _snapshotElapsed = 0.0f;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private NetzDebuggerClient _debugger;
#endif

        /// <summary>
        /// Number of clients connected to the server
        /// </summary>
        public int clientCount => _clients.Count;

        private NetzServer()
        {
            _clients = new List<ConnectedClient>(16);
            _driver = NetworkDriver.Create(new ReliableUtility.Parameters { WindowSize = 32 });
            _reliablePipeline = _driver.CreatePipeline(typeof(ReliableSequencedPipelineStage));

            _router.AddRoute(NetzGlobalMessages.Connect, OnClientConnectAck);
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

            return server;
        }

        internal void Update ()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (_debugger != null)
                _debugger.Update();
#endif

            _driver.ScheduleUpdate().Complete();

            // Clean up any disconnected clients
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
                using (var message = NetzMessage.Create(null, NetzGlobalMessages.Connect))
                {
                    var writer = message.BeginWrite();
                    writer.WriteUInt(client.id);
                    message.EndWrite(writer);

                    SendToClient(client, message);
                }
                
                // TODO: send message to all other clients informing them of the connection
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

                // If the object is spawning we first need to send a spawn message before we send the snapshow
                if(state == NetzObjectState.Spawning)
                {
                    using (var message = NetzMessage.Create(null, NetzGlobalMessages.Spawn))
                    {
                        var writer = message.BeginWrite();
                        writer.WriteULong(netobj.prefabHash);
                        writer.WriteULong(netobj.networkInstanceId);
                        writer.WriteTransform(netobj.transform);
                        // TODO: parent
                        message.EndWrite(writer);

                        SendToAllClients(message, includeHost: false);
                    }
                }

                using (var message = NetzMessage.Create(netobj, NetzGlobalMessages.Snapshot))
                {
                    var writer = message.BeginWrite();
                    netobj.WriteSnapshot(ref writer);
                    message.EndWrite(writer);

                    SendToAllClients(message, includeHost:false);
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

                    if(!netobj.isSceneObject)
                    {
                        using (var message = NetzMessage.Create(netobj, NetzGlobalMessages.Spawn))
                        {
                            var writer = message.BeginWrite();
                            writer.WriteULong(netobj.prefabHash);
                            writer.WriteULong(netobj.networkInstanceId);
                            writer.WriteTransform(netobj.transform);
                            // TODO: parent
                            message.EndWrite(writer);

                            SendToClient(client, message);
                        }
                    }

                    using (var message = NetzMessage.Create(netobj, NetzGlobalMessages.Snapshot))
                    {
                        var writer = message.BeginWrite();
                        netobj.WriteSnapshot(ref writer);
                        message.EndWrite(writer);

                        SendToClient(client, message);
                    }
                }

                client.state = NetzClientState.Connected;
            }
#endif
        }

        private void SendToClient(ConnectedClient client, NetzMessage message)
        {
            if (!_driver.IsCreated)
                return;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            SendToDebugger(client, message.id, length:message.length, received: false);
#endif

            _driver.BeginSend(_reliablePipeline, client.connection, out var clientWriter);
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
        private void OnClientConnectAck(FourCC messageId, ConnectedClient client, ref DataStreamReader reader)
        {
            var clientId = reader.ReadUInt();
            UnityEngine.Debug.Assert(client.id == clientId);

            // Is this client the host?
            client.isHost = client.id == NetzManager.instance.localClientId;

            if (client.isHost)
                client.state = NetzClientState.Connected;
            else
                client.state = NetzClientState.Synchronizing;
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