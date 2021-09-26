#if UNITY_COLLECTIONS && UNITY_TRANSPORT

using UnityEngine;
using Unity.Networking.Transport;
using System;
using System.Runtime.CompilerServices;
using System.Collections.Generic;

[assembly: InternalsVisibleTo("NoZ.Common.Editor")]

namespace NoZ.Netz
{
    /// <summary>
    /// Represents a networkable object
    /// </summary>
    public abstract class NetzObject : MonoBehaviour
    {
        internal const ulong FirstSpawnedObjectInstanceId = 1 << 24;

        [SerializeField] internal ulong _networkInstanceId = 0;
        [Tooltip("Optional string used to generate the prefab hash.  Set this if you have name collisions")]
        [SerializeField] string _prefabHashGenerator = null;

        private NetzObjectState _state = NetzObjectState.Unknown;

        public NetzObjectState state
        {
            get => _state;
            set
            {
                // Deboucne
                if (value == _state)
                    return;

                // Do not allow the dirty state to be set if the object is already marked as spawning
                if (_state == NetzObjectState.Spawning && value == NetzObjectState.Dirty)
                    return;

                _state = value;

                // If the object is spawning or dirty then make sure it is in the dirty list
                if (_state == NetzObjectState.Spawning || _state == NetzObjectState.Dirty)
                {
                    // If there dirty node was never allocated then it cant be in the list already
                    if (null == _dirtyNode)
                        _dirtyNode = new LinkedListNode<NetzObject>(this);
                    // If the dirty node is already in a list then we are good
                    else if (_dirtyNode.List != null)
                        return;

                    // Add to the dirty list
                    NetzObjectManager._dirtyObjects.AddLast(_dirtyNode);
                }
                // If the object is not dirty make sure it is not in the dirty list anymore
                else if (_dirtyNode != null && _dirtyNode.List != null)
                    NetzObjectManager._dirtyObjects.Remove(_dirtyNode);
            }
        }

        internal LinkedListNode<NetzObject> _dirtyNode;

        internal ulong prefabHash { get; set; }

        public ulong networkInstanceId => _networkInstanceId;

        public bool isClient => NetzManager.instance.isClient;
        public bool isServer => NetzManager.instance.isServer;
        public bool isSceneObject => (_networkInstanceId & NetzConstants.ObjectInstanceIdTypeMask) == 0;
        public bool isCustomObject => (_networkInstanceId & NetzConstants.ObjectInstanceIdTypeMask) == NetzConstants.CustomNetworkInstanceId;
        public bool isSpawnedObject => (_networkInstanceId & NetzConstants.ObjectInstanceIdTypeMask) == NetzConstants.SpawnedObjectInstanceId;

        /// <summary>
        /// Identifier of the client that owns this object
        /// </summary>
        public uint ownerClientId { get; private set; }

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
        /// Handle an incoming message sent to this object
        /// </summary>
        /// <param name="messageId">Message identifier</param>
        /// <param name="reader">Stream reader containing the data</param>
        internal void HandleMessage (FourCC messageId, ref DataStreamReader reader)
        {
            if(messageId == NetzConstants.Messages.Snapshot)
            {
                ReadSnapshot(ref reader);
                return;
            }
            
            // TODO: optional router
        }

        /// <summary>
        /// Marks the object as dirty to ensure its snapshot is rebuilt 
        /// </summary>
        public void SetDirty() => state = NetzObjectState.Dirty;

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
