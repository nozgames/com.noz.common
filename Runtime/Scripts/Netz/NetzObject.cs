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

        private NetzVariable[] _variables = null;

        internal uint _changedInSnapshot = 0;

        internal LinkedListNode<NetzObject> _dirtyNode;
        internal LinkedListNode<NetzObject> _node;

        internal ulong prefabHash { get; set; }

        public ulong networkInstanceId => _networkInstanceId;

        public bool isClient => NetzManager.instance.isClient;
        public bool isServer => NetzManager.instance.isServer;
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
        /// Return all fields on the object of NetzVariable type
        /// </summary>
        private static FieldInfo[] GetVariableFields(Type type)
        {
            if (!typeof(NetzObject).IsAssignableFrom(type))
                return new FieldInfo[0];

            if (_variableFieldsByType.TryGetValue(type, out var cachedFields))
                return cachedFields;

            var fields = new List<FieldInfo>();
            for (var rtype = type; rtype != typeof(NetzObject); rtype = rtype.BaseType)
                fields.AddRange(rtype.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).Where(f => typeof(NetzVariable).IsAssignableFrom(f.FieldType)));

            cachedFields = fields.ToArray();
            _variableFieldsByType.Add(type, cachedFields);
            return cachedFields;
        }

        private void Awake()
        {
            InitializeVariables();
        }

        private void InitializeVariables()
        {
            var fields = GetVariableFields(GetType());
            _variables = new NetzVariable[fields.Length];
            for (int i = 0; i < _variables.Length; i++)
            {
                var variable = _variables[i] = fields[i].GetValue(this) as NetzVariable;
                variable._parent = this;
            }
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
            else if (messageId == NetzConstants.Messages.Despawn)
            {
                NetzObjectManager.DespawnOnClient(networkInstanceId);
            }
        }

        internal void MarkChanged() => NetzObjectManager.MarkChanged(this);

        /// <summary>
        /// Start that is run when the object is synchronized on the network
        /// </summary>
        protected internal virtual void NetworkStart () { }

        protected internal virtual void OnDespawn() { }

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



        internal void Write (ref DataStreamWriter writer)
        {
            if (null == _variables)
                return;

            // Write all variables.  We dont need to write a count since the count
            // and order should match on the other side.
            for(int i=0; i<_variables.Length; i++)
                _variables[i].Write(ref writer);
        }

        internal void Read (ref DataStreamReader reader)
        {
            if (null == _variables)
                return;

            // Write all variables.  We dont need to write a count since the count
            // and order should match on the other side.
            for (int i = 0; i < _variables.Length; i++)
                _variables[i].Read(ref reader);
        }

        /// <summary>
        /// Called when a network variable changes in any way, including interpolation and extrapolation
        /// </summary>
        protected internal virtual void OnNetworkUpdate ()
        {
        }
    }
}

#endif
