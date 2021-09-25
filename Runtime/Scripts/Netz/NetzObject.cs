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
    public abstract class NetzObject : MonoBehaviour
    {
        [SerializeField] internal ulong _networkInstanceId = 0;

        public ulong networkInstanceId => _networkInstanceId;

        public bool isDirty { get; internal set; }

        public bool isClient => NetzManager.instance.isClient;
        public bool isServer => NetzManager.instance.isServer;

        public ulong OwnerClientId { get; private set; }

        /// <summary>
        /// Handle an incoming message sent to this object
        /// </summary>
        /// <param name="messageId">Message identifier</param>
        /// <param name="reader">Stream reader containing the data</param>
        internal void HandleMessage (FourCC messageId, ref DataStreamReader reader)
        {
            if(messageId == NetzGlobalMessages.Snapshot)
            {
                Debug.Log("Snapshot?");
                ReadSnapshot(ref reader);
            }

            //onNetworkMessage?.Invoke(messageId, reader);
        }

        /// <summary>
        /// Marks the object as dirty to ensure its snapshot is rebuilt 
        /// </summary>
        public void SetDirty() => NetzObjectManager.SetDirty(this);

        /// <summary>
        /// Write the objects snapshot to the given stream
        /// </summary>
        /// <param name="writer">Stream to write to</param>
        protected internal abstract void WriteSnapshot(ref DataStreamWriter writer);

        /// <summary>
        /// Read the object's snapshot from the given stream
        /// </summary>
        /// <param name="reader">Stream to read from</param>
        protected internal abstract void ReadSnapshot(ref DataStreamReader reader);
    }
}

#endif
