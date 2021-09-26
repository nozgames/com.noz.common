namespace NoZ.Netz
{
    public delegate void ClientStateChangeEvent (uint clientId, NetzClientState oldState, NetzClientState newState);

    public delegate void ServerStartedEvent ();

    public delegate void ServerStoppedEvent ();
}
