#if UNITY_COLLECTIONS && UNITY_TRANSPORT

using UnityEngine;
using Unity.Networking.Transport;
using System;

namespace NoZ.Netz
{
    internal unsafe class NetzClient : IDisposable
    {
        private const float KeepAliveDuration = 10.0f;

        private NetworkConnection _connection;
        private NetworkDriver _driver;
        private NetworkPipeline _reliablePipeline;
        private float _nextKeepAlive;
        private NetzMessageRouter<NetzClient> _router;

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
            _router.AddRoute(NetzGlobalMessages.Connect, OnConnectMessage);
            _router.AddRoute(NetzGlobalMessages.Spawn, OnSpawnMessage);
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

            // Make sure we send messages periodically to the server or it will drop us
            if (state == NetzClientState.Connected)
            {
                _nextKeepAlive -= Time.deltaTime;
                if (_nextKeepAlive < 0.0f)
                {
                    using (var message = NetzMessage.Create(null, NetzGlobalMessages.KeepAlive))
                    {
                        SendToServer(message);
                    }

                    _nextKeepAlive = KeepAliveDuration;
                }
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

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // Ignore connect message for debugger becuase it will send to the debugger itself
            if(messageId != NetzGlobalMessages.Connect)
                SendToDebugger(messageId, received: true, length: reader.Length);
#endif

            // No target, global messages
            if (networkInstanceId == 0)
            {
                _router.Route(messageId, this, ref reader);
            }
            else if (NetzObjectManager.TryGetObject(networkInstanceId, out var obj))
            {
                obj.HandleMessage(messageId, ref reader);
            }
        }

        internal void SendToServer (NetzMessage message)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            SendToDebugger(message.id, received: false, length:message.length);
#endif

            _driver.BeginSend(_reliablePipeline, connection, out var clientWriter);
            clientWriter.WriteBytes(message.buffer, message.length);
            _driver.EndSend(clientWriter);

            // Since we sent a message we can reset the keep alive
            _nextKeepAlive = KeepAliveDuration;
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

            using (var message = NetzMessage.Create(null, NetzGlobalMessages.Connect))
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
            var netzObject = NetzObjectManager.SpawnOnClient(reader.ReadULong());
            if (null == netzObject)
                return;

            netzObject._networkInstanceId = reader.ReadULong();
            reader.ReadTransform(netzObject.transform);
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
