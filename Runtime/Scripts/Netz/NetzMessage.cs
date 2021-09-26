#if UNITY_COLLECTIONS && UNITY_TRANSPORT

using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Networking.Transport;

namespace NoZ.Netz
{
    internal unsafe struct NetzMessage : IDisposable
    {
        private const int MaxPoolSize = 32;

        private struct PooledBuffer : IDisposable
        {
            private NativeArray<byte> _buffer;

            public bool isCreated => _buffer.IsCreated;

            public byte* ptr { get; private set; }

            public int capacity => _buffer.Length;

            public PooledBuffer (int size)
            {
                _buffer = new NativeArray<byte>(size, Allocator.Persistent);
                ptr = (byte*)NativeArrayUnsafeUtility.GetUnsafePtr(_buffer);
            }

            public void Dispose()
            {
                ptr = null;
                _buffer.Dispose();
            }
        }

        private static List<PooledBuffer> _pooledBuffers = new List<PooledBuffer>();

        internal static void Shutdown ()
        {
            foreach(var pooledWriter in _pooledBuffers)
                pooledWriter.Dispose();

            _pooledBuffers.Clear();
        }

        private PooledBuffer _buffer;
        private bool _writing;

        public FourCC id { get; private set; }
        
        /// <summary>
        /// Return the length of the message in bytes
        /// </summary>
        public int length { get; private set; }

        /// <summary>
        /// Return the unsafe buffer pointer to the message data
        /// </summary>
        public byte* buffer => _buffer.ptr;

        /// <summary>
        /// Create a new message.  Note that this message must be disposed or it will leak
        /// </summary>
        /// <param name="target">Target object</param>
        /// <param name="messageType">Message identifier</param>
        public static NetzMessage Create (NetzObject target, FourCC messageType)
        {
            var message = new NetzMessage();
            if (_pooledBuffers.Count > 0)
            {
                message._buffer = _pooledBuffers[_pooledBuffers.Count - 1];
                _pooledBuffers.RemoveAt(_pooledBuffers.Count - 1);
            }
            else
                message._buffer = new PooledBuffer(4096);

            // Write the message header
            var writer = message.BeginWrite();
            writer.WriteULong(target != null ? target.networkInstanceId : 0);
            writer.WriteFourCC(messageType);
            message.EndWrite(writer);

            message.id = messageType;

            return message;
        }

        /// <summary>
        /// Begin writing to the message.  Note that the returned writer must be passed to EndWrite to commit the writes        
        /// </summary>
        /// <returns>DataStreamWriter to write to</returns>
        public DataStreamWriter BeginWrite()
        {
            if (_writing)
                throw new InvalidOperationException("BeginWrite cannot be called multiple times without calling EndWrite");

            _writing = true;
            return new DataStreamWriter(buffer + length, _buffer.capacity - length);
        }

        /// <summary>
        /// End writing to the message
        /// </summary>
        /// <param name="writer"></param>
        public void EndWrite (DataStreamWriter writer) 
        {
            writer.Flush();
            length += writer.Length;
            _writing = false;
        }

        public void Dispose()
        {
            _writing = false;
            length = 0;

            if (_buffer.isCreated)
            {
                if (_pooledBuffers.Count >= MaxPoolSize)
                    _buffer.Dispose();
                else
                    _pooledBuffers.Add(_buffer);

                _buffer = default(PooledBuffer);
            }
        }
    }
}

#endif