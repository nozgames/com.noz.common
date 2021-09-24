#if UNITY_COLLECTIONS && UNITY_TRANSPORT

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace NoZ.Netz
{
    /// <summary>
    /// Static class that manages all network objects
    /// </summary>
    public static class NetzObjectManager
    {
        internal static readonly Dictionary<ulong, NetzObject> _objects = new Dictionary<ulong, NetzObject>();

        internal static readonly List<NetzObject> _dirtyObjects = new List<NetzObject>(128);

        public static bool TryGetObject (ulong networkInstanceId, out NetzObject obj) =>
            _objects.TryGetValue(networkInstanceId, out obj);

        internal static void RegisterSceneObjects ()
        {
            foreach(var obj in Object.FindObjectsOfType<NetzObject>())
            {
                _objects.Add(obj.networkInstanceId, obj);
            }
        }

        internal static void SetDirty (NetzObject obj)
        {
            if (obj.isDirty)
                return;

            obj.isDirty = true;
            _dirtyObjects.Add(obj);
        }
    }
}

#endif