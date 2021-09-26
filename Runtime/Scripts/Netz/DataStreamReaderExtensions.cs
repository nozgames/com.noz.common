#if UNITY_COLLECTIONS && UNITY_TRANSPORT

using Unity.Networking.Transport;
using UnityEngine;

namespace NoZ.Netz
{
    public static class DataStreamReaderExtensions
    {
        public static Vector3 ReadVector3(this DataStreamReader reader) =>
            new Vector3(reader.ReadFloat(), reader.ReadFloat(), reader.ReadFloat());

        public static Quaternion ReadQuaternion(this DataStreamReader reader) =>
            new Quaternion(reader.ReadFloat(), reader.ReadFloat(), reader.ReadFloat(), reader.ReadFloat());

        public static void ReadTransform (this DataStreamReader reader, Transform transform)
        {
            transform.position = ReadVector3(reader);
            transform.rotation = ReadQuaternion(reader);
        }
    }
}

#endif