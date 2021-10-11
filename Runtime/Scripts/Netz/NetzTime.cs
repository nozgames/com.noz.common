using UnityEngine;

namespace NoZ.Netz
{
    public static class NetzTime
    {
        /// <summary>
        /// Return the current server time
        /// </summary>
        public static float serverTime { get; internal set; }

        public static float snapshotTime { get; internal set; }

        public static float lastSnapshotTime { get; internal set; }
    }
}
