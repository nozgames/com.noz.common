namespace NoZ
{
    public static class HashCode
    {
        private const uint FNVOffsetBasis32 = 2166136261;
        private const uint FNVPrime32 = 16777619;

        private const ulong FNVOffsetBasis64 = 14695981039346656037;
        private const ulong FNVPrime64 = 1099511628211;

        /// <summary>
        /// Generates a 32-bit stable hash code for the string
        ///
        /// Implements FNV-1-32
        /// https://en.wikipedia.org/wiki/Fowler%E2%80%93Noll%E2%80%93Vo_hash_function
        /// </summary>
        /// <param name="value">String value to convert to hash</param>
        /// <returns>Sable 32-bit hash</returns>
        public static uint GetStableHash32(this string txt)
        {
            unchecked
            {
                uint hash = FNVOffsetBasis32;
                for (int i = 0; i < txt.Length; i++)
                {
                    uint ch = txt[i];
                    hash *= FNVPrime32;
                    hash ^= ch;
                }

                return hash;
            }
        }

        /// <summary>
        /// Generates a 64-bit stable hash code for the string
        ///
        /// Implements FNV-1-64
        /// https://en.wikipedia.org/wiki/Fowler%E2%80%93Noll%E2%80%93Vo_hash_function
        /// </summary>
        /// <param name="value">String value to convert to hash</param>
        /// <returns>Sable 64-bit hash</returns>
        public static ulong GetStableHash64(this string value)
        {
            unchecked
            {
                var hash = FNVOffsetBasis64;
                for (var i = 0; i < value.Length; i++)
                {
                    ulong ch = value[i];
                    hash *= FNVPrime64;
                    hash ^= ch;
                }

                return hash;
            }
        }
    }
}
