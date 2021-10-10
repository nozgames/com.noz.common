#if UNITY_COLLECTIONS && UNITY_TRANSPORT

namespace NoZ.Netz
{
    public enum NetzClientState 
    {
        Unknown,

        /// <summary>
        /// Client is attempting to connect to the server
        /// </summary>
        Connecting,

        /// <summary>
        /// Client has connected to the server
        /// </summary>
        Connected,

        /// <summary>
        /// Client is synchronizing state with the server
        /// </summary>
        Synchronizing,

        /// <summary>
        /// Client is synchronized and waiting for the server to send snapshots
        /// </summary>
        Synchronized,

        /// <summary>
        /// Client is synchronized and active in the world
        /// </summary>
        Active
    }
}

#endif
