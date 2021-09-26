using System;

namespace NoZ
{
    [Serializable]
    public struct FourCC : System.IEquatable<FourCC>
    {
        public uint value;

        public FourCC(uint value) => this.value = value;

        public FourCC (char a, char b, char c, char d)
        {
            value = (((uint)a) << 24) + (((uint)b) << 16) + (((uint)c) << 8) + ((uint)d);
        }

        public FourCC(byte a, byte b, byte c, byte d)
        {
            value = (((uint)a) << 24) + (((uint)b) << 16) + (((uint)c) << 8) + ((uint)d);
        }

        public bool Equals(FourCC other) => other.value == value;

        public override string ToString() => 
            $"{(char)((value >> 24) & 0xFF)}{(char)((value >> 16) & 0xFF)}{(char)((value >> 8) & 0xFF)}{(char)(value & 0xFF)}";

        public static bool operator ==(FourCC lhs, FourCC rhs) => lhs.value == rhs.value;

        public static bool operator != (FourCC lhs, FourCC rhs) => !(lhs == rhs);

        public override bool Equals(object obj) => this.Equals((FourCC)obj);

        public static implicit operator uint (FourCC v) => v.value;

        public override int GetHashCode() => (int)value;
    }
}
