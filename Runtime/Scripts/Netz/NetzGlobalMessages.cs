namespace NoZ.Netz
{
    internal static class NetzGlobalMessages 
    {
        /// <summary>
        /// Message sent by clients periodically to keep the connection alive
        /// </summary>
        public static readonly FourCC KeepAlive = new FourCC('K', 'E', 'E', 'P');

        /// <summary>
        /// Message sent to clients when a new client connects to the server
        /// </summary>
        public static readonly FourCC ClientConnected = new FourCC('C', 'L', 'C', ' ');

        /// <summary>
        /// Message sent to clients when a new client disconnects from the server
        /// </summary>
        public static readonly FourCC ClientDisconnected = new FourCC('C', 'L', 'D', ' ');

        /// <summary>
        /// Object snapshot
        /// </summary>
        public static readonly FourCC Snapshot = new FourCC('S', 'N', 'A', 'P');
    }
}
