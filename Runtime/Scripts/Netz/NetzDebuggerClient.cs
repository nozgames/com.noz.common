#if UNITY_COLLECTIONS && UNITY_TRANSPORT

using System;
using System.Collections.Generic;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Networking.Transport;
using UnityEngine;

namespace NoZ.Netz
{
    public unsafe class NetzDebuggerClient : IDisposable
    {
        private struct Message
        {
            public uint from;
            public uint to;
            public FourCC id;
            public bool received;
            public ushort length;
        }

        private const float KeepAliveDuration = 5.0f;
        private static readonly int MessageSize = UnsafeUtility.SizeOf<Message>();
        private static readonly int MaxMessagesPerSend = (NetworkParameterConstants.MTU * 90 / 100) / MessageSize;

        private NetworkConnection _connection;
        private NetworkPipeline _pipeline;
        private NetworkDriver _driver;
        private bool _connected;
        private float _nextKeepAlive;
        private List<Message> _queue = new List<Message>(32);
        private uint _id;

        private NetzDebuggerClient()
        {
        }

        public static NetzDebuggerClient Connect (NetworkEndPoint endpoint, uint id)
        {
            var client = new NetzDebuggerClient();
            client._driver = NetworkDriver.Create();
            client._pipeline = client._driver.CreatePipeline(typeof(ReliableSequencedPipelineStage));
            client._nextKeepAlive = KeepAliveDuration;
            client._connection = client._driver.Connect(endpoint);
            client._id = id;
            return client;
        }

        public void Update ()
        {
            if (!_driver.IsCreated)
                return;

            // TODO: retry connection

            _driver.ScheduleUpdate().Complete();

            if (!_connection.IsCreated)
                return;

            // Make sure we send messages periodically to the server or it will drop us
            if(_connected)
            {
                _nextKeepAlive -= Time.deltaTime;
                if (_nextKeepAlive < 0.0f)
                    SendKeepAlive();
            }

            DataStreamReader stream;
            NetworkEvent.Type cmd;

            while ((cmd = _connection.PopEvent(_driver, out stream)) != NetworkEvent.Type.Empty)
            {
                if (cmd == NetworkEvent.Type.Connect)
                {
                    _connected = true;

                    _driver.BeginSend(_pipeline, _connection, out var clientWriter);
                    clientWriter.WriteUInt(NetzGlobalMessages.Connect.value);
                    clientWriter.WriteUInt(_id);
                    _driver.EndSend(clientWriter);
                }
                else if (cmd == NetworkEvent.Type.Data)
                {
                    ReadMessage(stream);
                }
                else if (cmd == NetworkEvent.Type.Disconnect)
                {
                    _connected = false;
                    _connection = default(NetworkConnection);
                }
            }

            if (!_connected)
                return;

            // Send our queue
            while (_queue.Count > 0)
            {
                var count = Mathf.Min(_queue.Count, MaxMessagesPerSend);

                _driver.BeginSend(_pipeline, _connection, out var clientWriter);
                clientWriter.WriteUInt(NetzGlobalMessages.Debug.value);
                clientWriter.WriteUShort((ushort)count);
                for (int i=0; i<count; i++)
                {
                    var message = _queue[i];
                    clientWriter.WriteUInt(message.from);
                    clientWriter.WriteUInt(message.to);
                    clientWriter.WriteUInt(message.id.value);
                    clientWriter.WriteByte(message.received ? (byte)1 : (byte)0);
                    clientWriter.WriteUShort(message.length);
                }
                _queue.RemoveRange(0, count);

                _driver.EndSend(clientWriter);
            }
        }

        public void Send (uint from, uint to, FourCC messageId, int length, bool received=false)
        {
            var message = new Message { from = from, to = to, id = messageId, received = received, length = (ushort)length };
            _queue.Add(message);

            if (!_connected)
                return;

            _nextKeepAlive = KeepAliveDuration;
        }

        private void SendKeepAlive ()
        {
            _driver.BeginSend(_pipeline, _connection, out var clientWriter);
            clientWriter.WriteUInt(NetzGlobalMessages.KeepAlive.value);
            _driver.EndSend(clientWriter);

            _nextKeepAlive = KeepAliveDuration;
        }

        private void ReadMessage(DataStreamReader reader)
        {
        }

        public void Dispose()
        {
            if(_connection.IsCreated)
                _connection.Close(_driver);

            if(_driver.IsCreated)
                _driver.Dispose();
        }
    }
}

#endif