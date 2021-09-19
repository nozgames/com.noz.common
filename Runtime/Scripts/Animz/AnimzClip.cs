using System;
using UnityEngine;

namespace NoZ.Animz
{
    [CreateAssetMenu(menuName = "NoZ/Animz/Clip")]
    public class AnimzClip : ScriptableObject
    {
        [Serializable]
        internal struct AnimzEventFrame
        {
            [Tooltip("Normalized time to raise the event [0-1]")]
            public float time;

            [Tooltip("Event to raise")]
            public AnimzEvent raise;
        }

        [SerializeField] private AnimationClip _clip = null;
        [SerializeField] private float _speed = 1.0f;
        [SerializeField] private float _blendTime = 0.1f;
        [SerializeField] private AnimzEventFrame[] _events = null;

        public AnimationClip clip => _clip;
        public float speed => _speed;
        public float blendTime => _blendTime;
        
        internal AnimzEventFrame[] events => _events;
    }
}
