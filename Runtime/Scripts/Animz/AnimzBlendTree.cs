using System;
using UnityEngine;

namespace NoZ.Animz
{
    [CreateAssetMenu(menuName = "NoZ/Animz/BlendTree")]
    public class AnimzBlendTree : ScriptableObject
    {
        [Serializable]
        private struct Blend
        {
            public AnimationClip clip;
            public float position;
        }

        [SerializeField] private AnimationClip[] _clips = null;

        [SerializeField] private float _speed = 1.0f;
        [SerializeField] private float _blendTime = 0.1f;

        [SerializeField] private AnimzClip.AnimzEventFrame[] _events = null;

        internal AnimzClip.AnimzEventFrame[] events => _events;

        private void OnEnable()
        {
            isLooping = false;
            length = 0.0f;

            if (_clips == null)
                return;

            foreach(var clip in _clips)
            {
                isLooping |= clip.isLooping;
                length = Mathf.Max(clip.length, length);
            }
        }

        public bool isLooping { get; private set; }
        public float length { get; private set; }
        public float speed => _speed;
        public float blendTime => _blendTime;
    }
}
