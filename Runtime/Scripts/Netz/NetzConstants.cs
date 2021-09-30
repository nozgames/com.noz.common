#if UNITY_COLLECTIONS && UNITY_TRANSPORT

namespace NoZ.Netz
{
    public static class NetzConstants
    {
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

        public static class Messages
        {
            /// <summary>
            /// Connection message
            /// </summary>
            public static readonly FourCC Connect = new FourCC('C', 'O', 'N', 'N');

            /// <summary>
            /// Disconnect message
            /// </summary>
            public static readonly FourCC Disconnect = new FourCC('D', 'C', 'O', 'N');

            /// <summary>
            /// Message sent by clients periodically to keep the connection alive
            /// </summary>
            public static readonly FourCC KeepAlive = new FourCC('K', 'E', 'E', 'P');

            /// <summary>
            /// Object snapshot
            /// </summary>
            public static readonly FourCC Snapshot = new FourCC('S', 'N', 'A', 'P');

            /// <summary>
            /// Send to spawn an object on the clients
            /// </summary>
            public static readonly FourCC Spawn = new FourCC('S', 'P', 'W', 'N');


            /// <summary>
            /// Sent to Despawn an object on the client
            /// </summary>
            public static readonly FourCC Despawn = new FourCC('D', 'S', 'P', 'N');


            /// <summary>
            /// Debug message for debugger
            /// </summary>
            public static readonly FourCC Debug = new FourCC('D', 'B', 'U', 'G');

            /// <summary>
            /// Sends the current client state for all clients
            /// </summary>
            public static readonly FourCC ClientStates = new FourCC('C', 'L', 'S', 'T');

            /// <summary>
            /// Load a scene
            /// </summary>
            public static readonly FourCC LoadScene = new FourCC('L', 'D', 'S', 'N');
        }
    }
}

#endif