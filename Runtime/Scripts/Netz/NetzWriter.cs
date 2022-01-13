#if UNITY_COLLECTIONS && UNITY_TRANSPORT

using Unity.Networking.Transport;
using UnityEngine;

namespace NoZ.Netz
{
    public unsafe struct NetzWriter
    {
        internal DataStreamWriter _writer;

        public int length => _writer.Length;
        public int lengthInBits => _writer.LengthInBits;

        public NetzWriter (byte* buffer, int length)
        {
            _writer = new DataStreamWriter (buffer, length);
        }

        public NetzWriter (DataStreamWriter writer)
        {
            _writer = writer;
        }

        public void Flush() => _writer.Flush();

        public void WriteFixedString32 (string value) => _writer.WriteFixedString32(value);

        /// <summary>
        /// Write a floating point value to the stream
        /// </summary>
        public void WriteFloat(float value) => _writer.WriteFloat(value); //_writer.WritePackedFloat(value, NetzConstants.CompressionModel);

        public void WriteFloatDelta(float value, float baseline) => _writer.WritePackedFloatDelta(value, baseline, NetzConstants.CompressionModel);

        public void WriteUShort(ushort value) => _writer.WriteUShort(value);

        public void WriteULong(ulong value) => _writer.WritePackedULong(value, NetzConstants.CompressionModel);

        public void WriteULongDelta(ulong value, ulong baseline) => _writer.WritePackedULongDelta(value, baseline, NetzConstants.CompressionModel);

        public void WriteUInt(uint value) => _writer.WritePackedUInt(value, NetzConstants.CompressionModel);

        public void WriteInt(int value) => _writer.WritePackedInt(value, NetzConstants.CompressionModel);

        public void WriteByte(byte value) => _writer.WriteByte(value);

        public void WriteBit(bool value) => _writer.WriteRawBits(value ? 1U : 0U, 1);

        public void WriteVector3(Vector3 value)
        {
            _writer.WriteFloat(value.x);
            _writer.WriteFloat(value.y);
            _writer.WriteFloat(value.z);
        }

        public void WriteQuaternion(Quaternion value)
        {
            _writer.WriteFloat(value.x);
            _writer.WriteFloat(value.y);
            _writer.WriteFloat(value.z);
            _writer.WriteFloat(value.w);
        }
    }
}

#endif
