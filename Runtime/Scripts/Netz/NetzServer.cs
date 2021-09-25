#if UNITY_COLLECTIONS && UNITY_TRANSPORT

using System;
using System.Collections.Generic;
using System.Diagnostics;
using Unity.Collections;
using Unity.Networking.Transport;
using Unity.Networking.Transport.Utilities;
using UnityEngine.Assertions;

namespace NoZ.Netz
{
    internal unsafe class NetzServer : IDisposable
    {
        private class ConnectedClient
        {
            public NetworkConnection connection;
            public bool connected;
            public int lastSnapshotSent;
            public int lastSnapshotReceived;
            public uint id;
        }

        private NetworkDriver _driver;
        private NetworkPipeline _reliablePipeline;
        private List<ConnectedClient> _clients;
        private uint _nextClientId = 1;

        /// <summary>
        /// Number of clients connected to the server
        /// </summary>
        public int clientCount => _clients.Count;

        private NetzServer()
        {
            _clients = new List<ConnectedClient>(16);
            _driver = NetworkDriver.Create(new ReliableUtility.Parameters { WindowSize = 32 });
            _reliablePipeline = _driver.CreatePipeline(typeof(ReliableSequencedPipelineStage));
        }

        public void Dispose()
        {
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
                var client = new ConnectedClient { connection = c, id = _nextClientId++ };
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
                        _clients[i] = default(ConnectedClient);
                    }
                }
            }

            // Send any new snapshot for all dirty objects
            for (int i = NetzObjectManager._dirtyObjects.Count - 1; i >= 0; i--)
            {
                // Clear dirty state and remove from the dirty objects list
                var obj = NetzObjectManager._dirtyObjects[i];
                obj.isDirty = false;
                NetzObjectManager._dirtyObjects.RemoveAt(i);

                using (var message = NetzMessage.Create(obj, NetzGlobalMessages.Snapshot))
                {
                    var writer = message.BeginWrite();
                    obj.WriteSnapshot(ref writer);
                    message.EndWrite(writer);

                    SendToAllClients(message, includeHost:false);
                }
            }
        }

        private void SendToClient(ConnectedClient client, NetzMessage message)
        {
            if (!_driver.IsCreated)
                return;

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
                if (!client.connected)
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

            // No target, global messages
            if (networkInstanceId == 0)
            {
                // Connection ack from the client
                if(messageId == NetzGlobalMessages.Connect)
                {
                    var clientId = reader.ReadUInt();
                    Debug.Assert(client.id == clientId);
                    client.connected = true;
                }

                return;
            }

            if (NetzObjectManager.TryGetObject(networkInstanceId, out var obj))
            {
                obj.HandleMessage(messageId, ref reader);
            }
        }
    }
}

#endif