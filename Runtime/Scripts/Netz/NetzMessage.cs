#if UNITY_COLLECTIONS && UNITY_TRANSPORT

using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Networking.Transport;

namespace NoZ.Netz
{
    public enum NetzMessageRouting
    {
        Server,
        Client
    }

    public unsafe struct NetzMessage : IDisposable
    {
        private const int MaxPoolSize = 32;

        private struct PooledDataStreamWriter : IDisposable
        {
            public NativeArray<byte> buffer;
            public byte* ptr;
            public DataStreamWriter writer;

            public bool isNull => !writer.IsCreated;

            public PooledDataStreamWriter (int size)
            {
                buffer = new NativeArray<byte>(size, Allocator.Persistent);
                writer = new DataStreamWriter(buffer);
                ptr = (byte*)NativeArrayUnsafeUtility.GetUnsafePtr(buffer);
            }

            public void Dispose()
            {
                ptr = null;
                buffer.Dispose();
            }
        }

        private static List<PooledDataStreamWriter> _pooledWriters = new List<PooledDataStreamWriter>();

        internal static void Shutdown ()
        {
            foreach(var pooledWriter in _pooledWriters)
                pooledWriter.Dispose();

            _pooledWriters.Clear();
        }

        private PooledDataStreamWriter _writer;
        private NetzMessageRouting _routing;

        public DataStreamWriter writer => _writer.writer;

        /// <summary>
        /// Send a message with no accompanying data
        /// </summary>
        /// <param name="target">Target object</param>
        /// <param name="messageId">Message identifier</param>
        public static void Send(NetzObject target, FourCC messageId, NetzMessageRouting routing)
        {
            PooledDataStreamWriter writer;
            if (_pooledWriters.Count > 0)
                writer = _pooledWriters[_pooledWriters.Count - 1];
            else
                writer = new PooledDataStreamWriter(4096);

            using (BeginSend(target, messageId, routing))
            {
            }
        }

        /// <summary>
        /// Begin sending a message to the target object.  The messsage will be automatically
        /// send when it is disposed.
        /// </summary>
        /// <param name="target">Target object</param>
        /// <param name="messageId">Message identifer</param>
        /// <returns>Network message to write to</returns>
        public static NetzMessage BeginSend (NetzObject target, FourCC messageId, NetzMessageRouting routing)
        {
            PooledDataStreamWriter writer;
            if (_pooledWriters.Count > 0)
            {
                writer = _pooledWriters[_pooledWriters.Count - 1];
                _pooledWriters.RemoveAt(_pooledWriters.Count - 1);
            }
            else
                writer = new PooledDataStreamWriter(4096);

            writer.writer.Clear();
            writer.writer.WriteULong(target == null ? 0 : target.networkInstanceId);
            writer.writer.WriteUInt(messageId.value);

            return new NetzMessage { _writer = writer, _routing = routing };
        }

        unsafe public void Dispose()
        {
            if (_writer.isNull)
                return;

            _writer.writer.Flush();

            // Send the message to all clients.
            switch(_routing)
            {
                case NetzMessageRouting.Server:
                    NetzManager.instance.SendToServer((byte*)_writer.writer.AsNativeArray().GetUnsafeReadOnlyPtr(), _writer.writer.Length);
                    break;

                case NetzMessageRouting.Client:
                    NetzManager.instance.SendToAllClients((byte*)_writer.writer.AsNativeArray().GetUnsafeReadOnlyPtr(), _writer.writer.Length);
                    break;
            }

            if (_pooledWriters.Count >= MaxPoolSize)
            {
                _writer.Dispose();
                return;
            }

            _pooledWriters.Add(_writer);
        }

        /// <summary>
        /// Write a floating point value to the message
        /// </summary>
        /// <param name="value">Value</param>
        public void WriteFloat(float value) => _writer.writer.WriteFloat(value);
    }
}

#endif