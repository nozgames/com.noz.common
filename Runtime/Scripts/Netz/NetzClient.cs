#if UNITY_COLLECTIONS && UNITY_TRANSPORT

using UnityEngine;
using Unity.Networking.Transport;
using System;

namespace NoZ.Netz
{
    internal unsafe class NetzClient : IDisposable
    {
        private const float KeepAliveDuration = 1.0f;

        private NetworkConnection _connection;
        private NetworkDriver _driver;
        private NetworkPipeline _reliablePipeline;
        private float _nextKeepAlive;

        public NetworkConnection connection => _connection;

        /// <summary>
        /// Unique identifier of the client
        /// </summary>
        public uint id { get; private set; }

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
        }

        public static NetzClient Connect (NetworkEndPoint endpoint)
        {
            var client = new NetzClient ();
            client._connection = client._driver.Connect(endpoint);
            return client;
        }

        internal void Update ()
        {
            _driver.ScheduleUpdate().Complete();

            if (!_connection.IsCreated)
                return;

            // Make sure we send messages periodically to the server or it will drop us
            _nextKeepAlive -= Time.deltaTime;
            if (_nextKeepAlive < 0.0f)
            {
                using(var message = NetzMessage.Create (null, NetzGlobalMessages.KeepAlive))
                {
                    SendToServer(message);
                }

                _nextKeepAlive = KeepAliveDuration;
            }

            DataStreamReader stream;
            NetworkEvent.Type cmd;

            while ((cmd = connection.PopEvent(_driver, out stream)) != NetworkEvent.Type.Empty)
            {
                if (cmd == NetworkEvent.Type.Connect)
                {
                    // TODO: put us in a "connecting" state?  Need to wait for connect message and initial snapshot sync
                }
                else if (cmd == NetworkEvent.Type.Data)
                {
                    ReadMessage(stream);
                }
                else if (cmd == NetworkEvent.Type.Disconnect)
                {
                    _connection = default(NetworkConnection);
                }
            }
        }

        private void ReadMessage(DataStreamReader reader)
        {
            // Read the network object instance identifer
            var networkInstanceId = reader.ReadULong();

            // Read the message FourCC
            var messageId = new FourCC(reader.ReadUInt());

            // No target, global messages
            if (networkInstanceId == 0)
            {
                if(messageId == NetzGlobalMessages.Connect)
                {
                    id = reader.ReadUInt();

                    using (var message = NetzMessage.Create(null, NetzGlobalMessages.Connect))
                    {
                        var writer = message.BeginWrite();
                        writer.WriteUInt(id);
                        message.EndWrite(writer);
                        
                        SendToServer(message);
                    }
                }

                return;
            }

            if (NetzObjectManager.TryGetObject(networkInstanceId, out var obj))
            {
                obj.HandleMessage(messageId, ref reader);
            }
        }

        internal void SendToServer (NetzMessage message)
        {
            _driver.BeginSend(_reliablePipeline, connection, out var clientWriter);
            clientWriter.WriteBytes(message.buffer, message.length);
            _driver.EndSend(clientWriter);

            // Since we sent a message we can reset the keep alive
            _nextKeepAlive = KeepAliveDuration;
        }

        public void Dispose()
        {
            if (_connection.IsCreated)
                _connection.Close(_driver);

            if(_driver.IsCreated)
                _driver.Dispose();
        }
    }
}

#endif
