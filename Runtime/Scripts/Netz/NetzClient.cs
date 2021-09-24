#if UNITY_COLLECTIONS && UNITY_TRANSPORT

using Unity.Networking.Transport;

namespace NoZ.Netz
{
    public struct NetzClient
    {
        internal NetworkConnection _connection;

        public NetworkConnection connection => _connection;
        public bool IsLocal => false;
        public bool IsServer => false;
        public bool IsHost => false;
        public uint ID => 0;

        internal bool _connected;

        public bool isConnected => _connected;

        public NetzClient (NetworkConnection connection)
        {
            _connection = connection;
            _connected = false;
        }
    }
}

#endif
