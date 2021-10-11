using Unity.Networking.Transport;

namespace NoZ.Netz
{
    public unsafe struct NetzReader
    {
        internal DataStreamReader _reader;

        public int length => _reader.Length;
        public bool isEOF => _reader.HasFailedReads;

        public NetzReader (byte* buffer, int length)
        {
            _reader = new DataStreamReader(buffer, length);
        }

        public NetzReader (DataStreamReader reader)
        {
            _reader = reader;
        }

        /// <summary>
        /// Read a floating point value from the stream
        /// </summary>
        /// <returns></returns>
        public float ReadFloat() => _reader.ReadPackedFloat(NetzConstants.CompressionModel);

        public ulong ReadULong() => _reader.ReadPackedULong(NetzConstants.CompressionModel);

        public ulong ReadULongDelta(ulong baseline) => _reader.ReadPackedULongDelta(baseline, NetzConstants.CompressionModel);

        public uint ReadUInt() => _reader.ReadPackedUInt(NetzConstants.CompressionModel);

        public string ReadFixedString32() => _reader.ReadFixedString32().Value;
    }
}
