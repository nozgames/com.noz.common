#if UNITY_COLLECTIONS && UNITY_TRANSPORT

namespace NoZ.Netz
{
    public enum NetzClientState 
    {
        Unknown,
        Connecting,
        Synchronizing,
        Connected,
        Disconnecting,
        Disconnected
    }
}

#endif
