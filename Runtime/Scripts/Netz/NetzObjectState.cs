#if UNITY_COLLECTIONS && UNITY_TRANSPORT

using UnityEngine;

namespace NoZ.Netz
{
    public enum NetzObjectState
    {
        Unknown,
        Spawning,
        Spawned,
        Dirty
    }
}

#endif