using Unity.Networking.Transport;

namespace NoZ
{
    public abstract class NetzPlayer
    {
        public int id { get; internal set; }

        public bool isLocal { get; internal set; }

        public bool isHost { get; internal set; }

        public bool isConnected { get; internal set; }

        public abstract void Serialize(ref DataStreamWriter writer);

        public abstract void Deserialize(ref DataStreamReader reader);
    }
}
