using UnityEngine;

namespace NoZ
{
    public static class Vector3Extensions
    {
        public static Vector3 ToXZ (this Vector3 v) => new Vector3(v.x, 0.0f, v.z);

        public static Vector3 XYToXZ(this Vector3 v) => new Vector3(v.x, 0.0f, v.y);

        public static Vector2 ToVector2XZ(this Vector3 v) => new Vector2(v.x, v.z);

        /// <summary>
        /// Zero out the Y component of the vector
        /// </summary>
        /// <param name="vector">Source vector</param>
        /// <returns>Source vector with the Y value set to zero</returns>
        public static Vector3 ZeroY(this Vector3 vector) => new Vector3(vector.x, 0.0f, vector.z);
    }
}
