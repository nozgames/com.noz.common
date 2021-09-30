#if UNITY_COLLECTIONS && UNITY_TRANSPORT

namespace NoZ.Netz
{
    public delegate void ClientStateChangeEvent (uint clientId, NetzClientState oldState, NetzClientState newState);

    public delegate void ServerStartedEvent ();

    public delegate void ServerStoppedEvent ();

    public delegate void ServerStateChangedEvent(NetzServerState oldState, NetzServerState newState);
}

#endif