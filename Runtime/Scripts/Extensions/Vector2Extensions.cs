using UnityEngine;

namespace NoZ
{
    public static class Vector2Extensions
    {
        public static Vector2Int ToVector2Int(this Vector2 v) => new Vector2Int(Mathf.RoundToInt(v.x), Mathf.RoundToInt(v.y));
        public static Vector3 ToVector3XZ(this Vector2 v) => new Vector3(v.x, 0, v.y);

        public static Vector2 RotateYaw(this Vector2 v, float angle)
        {
            return new Vector2(
                v.x * Mathf.Cos(angle) - v.y * Mathf.Sin(angle),
                v.x * Mathf.Sin(angle) + v.y * Mathf.Cos(angle)
            );
        }
    }
}
