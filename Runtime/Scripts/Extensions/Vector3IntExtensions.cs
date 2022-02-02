using UnityEngine;

namespace NoZ
{
    public static class Vector3IntExtensions
    {
        public static Vector2Int ToVector2Int(this Vector3Int v) => new Vector2Int(v.x, v.y);

        public static Vector3 ToVector3(this Vector3Int v) => new Vector3(v.x, v.y, v.z);
    }
}
