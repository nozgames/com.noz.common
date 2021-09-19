using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace NoZ.Animz
{
    public class AnimzClipController : MonoBehaviour, IAnimationClipSource
    {
        [Tooltip("Optional Animator Target")]
        [SerializeField] private Animator _animator = null;

        [Tooltip("Animation clip to play")]
        [SerializeField] private AnimationClip _clip = null;

        [Tooltip("Current normalized time within the animation clip [0-1]")]
        [SerializeField] [Range(0, 1)] private float _time = 0.0f;

        private PlayableGraph _playableGraph;
        private AnimationPlayableOutput _playableOutput;
        private AnimationClipPlayable _playableClip;

        /// <summary>
        /// Get/Set the normalized time [0-1] of the animation
        /// </summary>
        public float time
        {
            get => _time;
            set
            {
                var time = Mathf.Clamp01(value);
                if (time == _time)
                    return;

                _time = time;

                UpdateGraph();
            }
        }

        private void Awake()
        {
            if (null == _animator)
                _animator = GetComponentInParent<Animator>();

            _playableGraph = PlayableGraph.Create();
            _playableGraph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            _playableOutput = AnimationPlayableOutput.Create(_playableGraph, "Animation", _animator);

            if (null != _clip)
            {
                _playableClip = AnimationClipPlayable.Create(_playableGraph, _clip);
                _playableOutput.SetSourcePlayable(_playableClip);
            }

            UpdateGraph();
        }

        private void OnDestroy()
        {
            _playableGraph.Destroy();
        }

        /// <summary>
        /// Allow the selected clip to be edited in the animation window
        /// </summary>
        void IAnimationClipSource.GetAnimationClips(List<AnimationClip> results)
        {
            if (null == _clip)
                return;

            results.Add(_clip);
        }

        private void UpdateGraph()
        {
            if (_playableClip.IsNull())
                return;

            _playableClip.SetTime(_time);
            _playableGraph.Evaluate();
        }

#if UNITY_EDITOR
        private void OnValidate() => UpdateGraph();
#endif
    }
}
