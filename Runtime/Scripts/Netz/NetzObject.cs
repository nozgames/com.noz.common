#if UNITY_COLLECTIONS && UNITY_TRANSPORT

using UnityEngine;
using Unity.Networking.Transport;
using System;
using System.Runtime.CompilerServices;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;

[assembly: InternalsVisibleTo("NoZ.Common.Editor")]

namespace NoZ.Netz
{
    /// <summary>
    /// Represents a networkable object
    /// </summary>
    public abstract class NetzObject : MonoBehaviour
    {
        private static Dictionary<Type, FieldInfo[]> _variableFieldsByType = new Dictionary<Type, FieldInfo[]>();

        internal const ulong FirstSpawnedObjectInstanceId = 1 << 24;

        [SerializeField] internal ulong _networkInstanceId = 0;
        [Tooltip("Optional string used to generate the prefab hash.  Set this if you have name collisions")]
        [SerializeField] string _prefabHashGenerator = null;

        /// <summary>
        /// Snapshot the object was last changed in
        /// </summary>
        internal uint _changedInSnapshot = 0;

        /// <summary>
        /// Node that links this object into global list of network objects
        /// </summary>
        internal LinkedListNode<NetzObject> _node;

        internal ulong prefabHash { get; set; }

        public ulong networkInstanceId => _networkInstanceId;

        public bool isClient => !NetzServer.isCreated && NetzClient.isCreated;
        public bool isServer => NetzServer.isCreated;
        public bool isSceneObject => (_networkInstanceId & NetzConstants.ObjectInstanceIdTypeMask) == 0;
        public bool isCustomObject => (_networkInstanceId & NetzConstants.ObjectInstanceIdTypeMask) == NetzConstants.CustomNetworkInstanceId;
        public bool isSpawnedObject => (_networkInstanceId & NetzConstants.ObjectInstanceIdTypeMask) == NetzConstants.SpawnedObjectInstanceId;

        public bool isOwnedByLocalClient => ownerClientId == (NetzClient.instance?.id ?? uint.MaxValue);

        /// <summary>
        /// Identifier of the client that owns this object
        /// </summary>
        public uint ownerClientId { get; internal set; }

        protected NetzObject()
        {
            _node = new LinkedListNode<NetzObject>(this);
        }

        /// <summary>
        /// Generate the object's prefab hash
        /// </summary>
        /// <returns>Generated prefab hash</returns>
        internal ulong GeneratePrefabHash ()
        {
            prefabHash = (_prefabHashGenerator ?? name).GetStableHash64();
            return prefabHash;
        }

        /// <summary>
        /// Start that is run when the object is synchronized on the network
        /// </summary>
        protected internal virtual void NetworkStart () { }

        protected internal virtual void OnDespawn() { }

        protected internal virtual void OnNetworkUpdate () { }

        /// <summary>
        /// Send the object's entire state to the client
        /// </summary>
        public void SendToClient ()
        {
            var writer = NetzServer.instance.BeginSendEvent(this, 0);
            Write(ref writer);
            NetzServer.instance.EndSendEvent(writer);
        }

        /// <summary>
        /// Send a dataless event
        /// </summary>
        /// <param name="tag">Event tag</param>
        public void SendEventToClient (ushort tag)
        {
            NetzServer.instance.EndSendEvent(NetzServer.instance.BeginSendEvent(this, tag));
        }

        public NetzWriter BeginSendEventToClient (ushort tag)
        {
            if (!NetzServer.isCreated)
                throw new InvalidOperationException("BeginSendEventToClient must be called on a server");

            return NetzServer.instance.BeginSendEvent(this, tag);
        }

        public void EndSendEvent (NetzWriter writer)
        {

        }

        internal void ReadEvent (ushort tag, ref NetzReader reader)
        {
            if (tag == 0)
                Read(ref reader);
            else
                OnMessage(tag, ref reader);
        }

        public abstract void Read(ref NetzReader reader);

        public abstract void Write(ref NetzWriter writer);

        protected internal virtual void OnMessage (ushort tag, ref NetzReader reader)
        {
        }
    }
}

#endif
