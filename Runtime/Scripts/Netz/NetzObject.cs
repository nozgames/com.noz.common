#if UNITY_COLLECTIONS && UNITY_TRANSPORT

using UnityEngine;
using Unity.Networking.Transport;
using Unity.Collections;
using System;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("NoZ.Common.Editor")]

namespace NoZ.Netz
{
    /// <summary>
    /// Represents a networkable object
    /// </summary>
    public class NetzObject : MonoBehaviour
    {
        [SerializeField] internal ulong _networkInstanceId = 0;

        public ulong networkInstanceId => _networkInstanceId;

        public bool isClient => NetzManager.instance.IsClient;
        public bool isServer => NetzManager.instance.IsServer;

        public event Action<FourCC, DataStreamReader> onNetworkMessage;

        internal void HandleMessage (FourCC messageId, DataStreamReader reader)
        {
            onNetworkMessage?.Invoke(messageId, reader);
        }
    }
}

#endif
