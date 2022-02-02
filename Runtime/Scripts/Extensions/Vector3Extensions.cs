using UnityEngine;

namespace NoZ
{
    public static class Vector3Extensions
    {
        public static Vector3 ToXZ (this Vector3 v) => new Vector3(v.x, 0.0f, v.z);

        public static Vector3 XYToXZ(this Vector3 v) => new Vector3(v.x, 0.0f, v.y);

        public static Vector2 ToVector2XZ(this Vector3 v) => new Vector2(v.x, v.z);
    }
}
