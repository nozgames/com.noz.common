#if UNITY_COLLECTIONS && UNITY_TRANSPORT

using Unity.Networking.Transport;
using UnityEngine;

namespace NoZ.Netz
{
    public static class DataStreamWriterExtensions
    {
        public static void WriteVector3 (this ref DataStreamWriter writer, Vector3 vector3)
        {
            writer.WriteFloat(vector3.x);
            writer.WriteFloat(vector3.y);
            writer.WriteFloat(vector3.z);
        }

        public static void WriteQuaternion (this ref DataStreamWriter writer, Quaternion quaternion)
        {
            writer.WriteFloat(quaternion.x);
            writer.WriteFloat(quaternion.y);
            writer.WriteFloat(quaternion.z);
            writer.WriteFloat(quaternion.w);
        }

        public static void WriteTransform (this ref DataStreamWriter writer, Transform transform)
        {
            writer.WriteVector3(transform.position);
            writer.WriteQuaternion(transform.rotation);
        }
    }
}

#endif