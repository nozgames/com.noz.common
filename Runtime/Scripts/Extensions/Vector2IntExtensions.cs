using UnityEngine;

namespace NoZ
{
    public static class Vector2IntExtensions
    {
        public static Vector3Int ToVector3Int(this Vector2Int v) => new Vector3Int(v.x, v.y, 0);
        public static Vector3 ToVector3XZ(this Vector2Int v) => new Vector3(v.x, 0, v.y);
    }
}
