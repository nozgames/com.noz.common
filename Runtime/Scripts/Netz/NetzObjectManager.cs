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
        private static readonly Dictionary<ulong, NetzObject> _objects = new Dictionary<ulong, NetzObject>();

        public static bool TryGetObject (ulong networkInstanceId, out NetzObject obj) =>
            _objects.TryGetValue(networkInstanceId, out obj);

        internal static void RegisterSceneObjects ()
        {
            foreach(var obj in Object.FindObjectsOfType<NetzObject>())
            {
                _objects.Add(obj.networkInstanceId, obj);
            }
        }
    }
}

#endif