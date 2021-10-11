namespace NoZ.Netz
{
    public abstract class NetzPlayer
    {
        public int id { get; internal set; }

        public bool isLocal { get; internal set; }

        public bool isHost { get; internal set; }

        public bool isConnected { get; internal set; }

        public abstract void Write(ref NetzWriter writer);

        public abstract void Read(ref NetzReader reader);
    }
}
