using System;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace NoZ.Netz
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal struct ReliableEventInfo
    {
        public static readonly int StructSize = UnsafeUtility.SizeOf<ReliableEventInfo>();

        /// <summary>
        /// Incrementing event sequence number
        /// </summary>
        public uint id;

        /// <summary>
        /// Event tag
        /// </summary>
        public ushort tag;

        /// <summary>
        /// Size of the event in bytes
        /// </summary>
        public ushort size;

        /// <summary>
        /// Target nextwork object identifer
        /// </summary>
        public ulong target;
    }

    internal unsafe class ReliableEventQueue : IDisposable
    {
        protected NativeArray<byte> _buffer;
        protected byte* _bufferPtr;
        protected int _bufferSize;
        protected int _bufferSeek;

        protected NativeArray<ReliableEventInfo> _events;
        protected ReliableEventInfo* _eventsPtr;
        protected int _head = 0;
        protected int _count = 0;

        public int count => _count;

        protected ReliableEventQueue(int capacity, int maxBufferSize)
        {
            _buffer = new NativeArray<byte>(maxBufferSize, Allocator.Persistent);
            _bufferPtr = (byte*)NativeArrayUnsafeUtility.GetUnsafePtr(_buffer);
            _bufferSize = 0;
            _events = new NativeArray<ReliableEventInfo>(capacity, Allocator.Persistent);
            _eventsPtr = (ReliableEventInfo*)NativeArrayUnsafeUtility.GetUnsafePtr(_events);
        }

        protected ReliableEventInfo GetEventInfo(int index) => 
            _events[index % _events.Length];

        protected void SetEventInfo(int index, ReliableEventInfo evtinfo) =>
            _events[index % _events.Length] = evtinfo;

        public void Clear ()
        {
            _bufferSize = 0;
            _bufferSeek = 0;
            _count = 0;
            _head = 0;
        }

        public void Dispose()
        {
            _buffer.Dispose();
            _events.Dispose();
        }
    }
}

