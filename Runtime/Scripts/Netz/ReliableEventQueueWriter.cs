#if UNITY_COLLECTIONS && UNITY_TRANSPORT

using Unity.Collections.LowLevel.Unsafe;
using Unity.Networking.Transport;
using UnityEngine;

namespace NoZ.Netz
{
    internal unsafe class ReliableEventQueueWriter : ReliableEventQueue
    {
        private uint _nextEventId = 1;

        public uint lastAcknowledgedId { get; private set; }

        public ReliableEventQueueWriter(int capacity, int maxBufferSize) : base(capacity, maxBufferSize)
        {
        }

        /// <summary>
        /// Enqueue all events from the given qeueue into the new queue
        /// </summary>
        /// <param name="queue"></param>
        public void Enqueue (ReliableEventQueueWriter queue)
        {
            if (queue._bufferSize > 0)
            {
                UnsafeUtility.MemCpy(_bufferPtr + _bufferSize, queue._bufferPtr, queue._bufferSize);
                _bufferSize += queue._bufferSize;
            }

            for(int i=0; i<queue._count; i++)
            {
                var srcinfo = queue.GetEventInfo(queue._head + i);
                srcinfo.id = _nextEventId++;
                SetEventInfo(_head + _count, srcinfo);
                _count++;
            }
        }

        public void Enqueue (ulong targetObjectId, ushort tag)
        {
            EndEnqueue(BeginEnqueue(targetObjectId, tag));
        }

        public NetzWriter BeginEnqueue (ulong targetObjectId, ushort tag)
        {
            var evtinfo = GetEventInfo(_head + _count);
            evtinfo.tag = tag;
            evtinfo.target = targetObjectId;
            evtinfo.id = _nextEventId;
            evtinfo.size = 0;
            SetEventInfo(_head + _count, evtinfo);

            return new NetzWriter(_bufferPtr + _bufferSize, _buffer.Length - _bufferSize);
        }

        public uint EndEnqueue (NetzWriter writer)
        {
            var evtinfo = GetEventInfo(_head + _count);
            evtinfo.size = (ushort)writer.length;
            SetEventInfo(_head + _count, evtinfo);
            _bufferSize += evtinfo.size;
            _count++;
            _nextEventId++;

            return evtinfo.id;
        }

        /// <summary>
        /// Write the entire event buffer to the given writer
        /// </summary>
        public void Write (ref DataStreamWriter writer)
        {
            // Write all of the event infos starting from the head
            var ecnt = Mathf.Min(_count, ushort.MaxValue);
            writer.WriteUShort((ushort)ecnt);

            for (int i = 0; i < ecnt; i++)
            {
                var eptr = _eventsPtr + ((_head + i) % _events.Length);
                if (eptr->id == 0)
                    Debug.LogError("Invalid sequence");
                writer.WriteBytes((byte*)eptr, ReliableEventInfo.StructSize);
            }

            // Write the buffer
            writer.WriteUShort((ushort)_bufferSize);
            writer.WriteBytes(_bufferPtr, _bufferSize);
        }

        /// <summary>
        /// Acknowledge receipt of all events up to and including the given event identifier
        /// </summary>
        /// <param name="acknowledgeEventId">Event to acknowledge</param>
        public void Acknowledge (uint acknowledgeEventId)
        {
            if (acknowledgeEventId == 0 || acknowledgeEventId < lastAcknowledgedId)
                return;

            var bufferShift = 0;
            while(_count > 0)
            {
                var evtinfo = GetEventInfo(_head);
                if (evtinfo.id >= acknowledgeEventId)
                    break;

                _head = (_head + 1) % _events.Length;
                _count--;
                bufferShift += evtinfo.size;
            }

            if (bufferShift > 0)
            {
                UnsafeUtility.MemCpy(_bufferPtr, _bufferPtr + bufferShift, _bufferSize - bufferShift);
                _bufferSize -= bufferShift;
            }

            lastAcknowledgedId = acknowledgeEventId;
        }
    }
}


#endif