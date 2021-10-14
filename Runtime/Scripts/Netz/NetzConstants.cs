#if UNITY_COLLECTIONS && UNITY_TRANSPORT

using Unity.Networking.Transport;

namespace NoZ.Netz
{
    public static class NetzConstants
    {
        public static readonly NetworkCompressionModel CompressionModel = new NetworkCompressionModel(Unity.Collections.Allocator.Persistent);

        /// <summary>
        /// First object instance identifier for scene objects
        /// </summary>
        public const ulong SceneObjectInstanceId = 1;

        /// <summary>
        /// First object instance identifier for objects spawned at runtime
        /// </summary>
        public const ulong SpawnedObjectInstanceId = ((ulong)1 << 63);

        /// <summary>
        /// First object instance identifier for custom objects.  Custom object identifier are controlled
        /// by the application.
        /// </summary>
        public const ulong CustomNetworkInstanceId = SpawnedObjectInstanceId | ((ulong)1 << 62);

        /// <summary>
        /// Mask used to determine the type of object based on the instance id
        /// </summary>
        internal const ulong ObjectInstanceIdTypeMask = ((ulong)1 << 63) | ((ulong)1 << 62);

        /// <summary>
        /// How often a connect message is sent from server to client 
        /// </summary>
        internal const double ConnectMessageResendInterval = 1.0;

        /// <summary>
        /// How long the server will wait for a client to respond to a connect message
        /// </summary>
        internal const double ConnectTimeout = 10.0;

        /// <summary>
        /// How often a synchronize message can be sent
        /// </summary>
        internal const double SynchronizeInterval = 1.0f;

        /// <summary>
        /// How often to send keep alive message
        /// </summary>
        internal const float KeepAliveInterval = 5.0f;

        /// <summary>
        /// Maximum message size
        /// </summary>
        internal const int MaxMessageSize = 4096;


        internal const int MaxReliableEvents = 1024;
        internal const int ReliableEventBufferSize = 8192;

        internal static class GlobalTag 
        {
            public const ushort Connect = 1;

            public const ushort Synchronize = 2;

            public const ushort LoadScene = 3;

            public const ushort PlayerInfo = 4;

            public const ushort PlayerDisconnect = 5;

            public const ushort Instantiate = 6;

            public const ushort Destroy = 7;
        }
    }
}

#endif