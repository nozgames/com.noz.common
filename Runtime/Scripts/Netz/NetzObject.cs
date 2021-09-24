#if UNITY_COLLECTIONS && UNITY_TRANSPORT

using UnityEngine;
using Unity.Networking.Transport;
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

        public bool isDirty { get; internal set; }

        public bool isClient => NetzManager.instance.IsClient;
        public bool isServer => NetzManager.instance.IsServer;

        public ulong OwnerClientId { get; private set; }

        public event Action<FourCC, DataStreamReader> onNetworkMessage;

        /// <summary>
        /// Handle an incoming message sent to this object
        /// </summary>
        /// <param name="messageId">Message identifier</param>
        /// <param name="reader">Stream reader containing the data</param>
        internal void HandleMessage (FourCC messageId, DataStreamReader reader)
        {
            // Snapshot?
            if(messageId == NetzGlobalMessages.Snapshot)
            {
                HandleSnapshot(reader);
                return;
            }

            onNetworkMessage?.Invoke(messageId, reader);
        }

        /// <summary>
        /// Marks the object as dirty to ensure its snapshot is rebuilt 
        /// </summary>
        public void SetDirty() => NetzObjectManager.SetDirty(this);

        public NetzMessage BuildSnapshot ()
        {
            var msg = NetzMessage.BeginSend(this, NetzGlobalMessages.Snapshot, NetzMessageRouting.Client);
            BuildSnapshot(ref msg);
            return msg;
        }

        protected virtual void BuildSnapshot (ref NetzMessage writer)
        {
        }

        public virtual void HandleSnapshot(DataStreamReader reader)
        {
        }
    }
}

#endif
