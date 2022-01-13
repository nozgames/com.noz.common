#if UNITY_COLLECTIONS && UNITY_TRANSPORT

using System;
using Unity.Networking.Transport;
using UnityEngine;

namespace NoZ.Netz
{
    public unsafe struct ReliableEvent
    {
        internal byte* _bufferPtr;
        internal int _bufferSize;
        internal ushort _tag;
        internal ulong _target;
        internal uint _sequenceId;

        public ushort tag => _tag;
        public ulong target => _target;
        public uint sequenceId => _sequenceId;

        public NetzReader GetReader(int offset = 0)
        {
            if (offset > _bufferSize)
                throw new ArgumentException("Offset is past the end of the event data");

            return new NetzReader(_bufferPtr + offset, _bufferSize - offset);
        }
    }

    internal unsafe class ReliableEventQueueReader : ReliableEventQueue
    {
        private uint _lastDequeuedSequenceId = 0;

        public uint lastDequeuedSequenceId => _lastDequeuedSequenceId;

        public ReliableEventQueueReader(int capacity, int maxBufferSize) : base(capacity, maxBufferSize)
        {
        }

        /// <summary>
        /// Peek at the next event in the queue
        /// </summary>
        public ReliableEvent Peek()
        {
            if (_count == 0)
                throw new InvalidOperationException("Queue is empty");

            var evtinfo = _events[_head];
            return new ReliableEvent
            {
                _bufferPtr = _bufferPtr + _bufferSeek,
                _sequenceId = evtinfo.id,
                _tag = evtinfo.tag,
                _bufferSize = evtinfo.size,
                _target = evtinfo.target
            };
        }

        public ReliableEvent Dequeue ()
        {
            if (_count == 0)
                throw new InvalidOperationException("Queue is empty");

            var evtinfo = _events[_head];
            var evtptr = _bufferPtr + _bufferSeek;
            _count--;
            _head++;
            _bufferSeek += evtinfo.size;

            _lastDequeuedSequenceId = evtinfo.id;

            return new ReliableEvent
            {
                _bufferPtr = evtptr,
                _sequenceId = evtinfo.id,
                _tag = evtinfo.tag,
                _bufferSize = evtinfo.size,
                _target = evtinfo.target
            };
        }

        /// <summary>
        /// Overwrite the current event queue with the data in the given reader
        /// </summary>
        public void Read (ref DataStreamReader reader)
        {
            // Read all of the infos first
            _count = Mathf.Min(reader.ReadUShort(), _events.Length);
            if (count == 0)
                return;

            reader.ReadBytes((byte*)_eventsPtr, _count * ReliableEventInfo.StructSize);

            // Read the event buffer next
            _bufferSize = reader.ReadUShort();
            reader.ReadBytes(_bufferPtr, _bufferSize);

            _head = 0;
            _bufferSeek = 0;

            // Skip all events we have already read.
            while(_count > 0)
            {
                var evtinfo = GetEventInfo(_head);
                if (evtinfo.id > _lastDequeuedSequenceId)
                    break;

                _bufferSeek += evtinfo.size;
                _head++;
                _count--;
            }
        }
    }
}

#endif