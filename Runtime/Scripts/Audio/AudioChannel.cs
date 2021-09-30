using UnityEngine;

namespace NoZ
{
    [CreateAssetMenu(menuName = "NoZ/Audio/Audio Channel")]
    public class AudioChannel : ScriptableObject
    {
        [Range(0, 1)]
        public float volume = 1.0f;

        [Range(0, 2)]
        public float pitch = 1.0f;
    }
}
