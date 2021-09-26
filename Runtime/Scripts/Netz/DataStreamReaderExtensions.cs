#if UNITY_COLLECTIONS && UNITY_TRANSPORT

using Unity.Networking.Transport;
using UnityEngine;

namespace NoZ.Netz
{
    public static class DataStreamReaderExtensions
    {
        public static Vector3 ReadVector3(this ref DataStreamReader reader) =>
            new Vector3(reader.ReadFloat(), reader.ReadFloat(), reader.ReadFloat());

        public static Quaternion ReadQuaternion(this ref DataStreamReader reader) =>
            new Quaternion(reader.ReadFloat(), reader.ReadFloat(), reader.ReadFloat(), reader.ReadFloat());

        public static void ReadTransform (this ref DataStreamReader reader, Transform transform)
        {
            transform.position = reader.ReadVector3();
            transform.rotation = reader.ReadQuaternion();
        }

        public static FourCC ReadFourCC(this ref DataStreamReader reader) => new FourCC(reader.ReadUInt());
    }
}

#endif