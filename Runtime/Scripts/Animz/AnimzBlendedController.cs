using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Animations;
using System.Collections.Generic;
using System;

namespace NoZ.Animz
{
    public class AnimzBlendedController : MonoBehaviour, IAnimationClipSource
    {
        public delegate void AnimationCompleteDelegate(AnimationClip clip);
        public delegate void AnimationFrameDelegate (float normalizedTime);
        public delegate void AnimationEventDelegate(AnimzEvent evt);

        private struct Blend
        {
            public AnimationClip clip;
            public AnimzClip.AnimzEventFrame[] events;
            public AnimationClipPlayable playable;
            public float elapsed;
            public float duration;
            public float weight;
            public float weightStart;
            public float weightEnd;
            public double eventTime;
            public AnimationCompleteDelegate onComplete;
            public AnimationFrameDelegate onFrame;
            public AnimationEventDelegate onEvent;

            public bool isBlendingOut => weightEnd < MaxBlendWeight;
            public bool isBlendingIn => !isBlendingOut;
            public bool isDone => !clip.isLooping && playable.IsDone();
            public bool isPlaying => playable.GetPlayState() == PlayState.Playing;
            public float normalizedTime => (float)((playable.GetTime() % clip.length) / clip.length);
        }

        private class Blender : PlayableBehaviour
        {
            public AnimzBlendedController _controller = null;

            public override void PrepareFrame(Playable playable, FrameData info)
            {
                _controller.UpdateBlends(info.deltaTime);
                base.PrepareFrame(playable, info);
            }
        }

        private const float DefaultBlendTime = 0.1f;
        private const float MinBlendWeight = 0.0001f;
        private const float MaxBlendWeight = 1.0f;

        private PlayableGraph _playableGraph;
        private AnimationPlayableOutput _playableOutput;
        private AnimationMixerPlayable _mixer;

        [SerializeField] private Animator _animator = null;
        [SerializeField] private AnimationClip[] _clips = null;

        private Dictionary<int, int> _clipInstanceIdToIndex;
        private Blend[] _blends = null;
        private ScriptPlayable<Blender> _blender;

        public event AnimationEventDelegate onEvent = null;

        private void Awake()
        {
            if (null == _animator)
                _animator = GetComponent<Animator>();

            _playableGraph = PlayableGraph.Create();
            _playableGraph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);

            _blender = ScriptPlayable<Blender>.Create(_playableGraph, 1);
            _blender.GetBehaviour()._controller = this;

            _mixer = AnimationMixerPlayable.Create(_playableGraph, _clips.Length);

            _playableGraph.Connect(_mixer, 0, _blender, 0);

            _playableOutput = AnimationPlayableOutput.Create(_playableGraph, "Animation", _animator);
            _playableOutput.SetSourcePlayable(_blender);

            _clipInstanceIdToIndex = new Dictionary<int, int>();
            _blends = new Blend[_clips.Length];

            for (var i=0; i < _clips.Length; i++)
            {
                var clip = _clips[i];
                var blend = new Blend
                {
                    clip = clip,
                    playable = AnimationClipPlayable.Create(_playableGraph, clip)
                };

                _blends[i] = blend;

                if (!clip.isLooping)
                {
                    blend.playable.SetDuration(clip.length);
                    blend.playable.SetDone(true);
                }

                _playableGraph.Connect(blend.playable, 0, _mixer, i);
                _clipInstanceIdToIndex[clip.GetInstanceID()] = i;
                _mixer.SetInputWeight(i, 0.0f);
                blend.playable.Pause();
            }
        }

        private void OnEnable()
        {
            for(int i=_blends.Length-1; i>=0; i--)
            {
                if(_blends[i].isPlaying)
                {
                    _playableGraph.Play();
                    break;
                }
            }            
        }

        private void OnDisable()
        {
            _playableGraph.Stop();
        }

        private void OnDestroy()
        {
            _playableGraph.Destroy();
        }

        public void StopAll (float blendTime = DefaultBlendTime)
        {
            for (int i = _blends.Length - 1; i >= 0; i--)
            {
                ref var blend = ref _blends[i];
                if (blend.isBlendingOut)
                    continue;

                blend.duration = blendTime;
                blend.elapsed = 0.0f;
                blend.weightStart = blend.weight;
                blend.weightEnd = MinBlendWeight;
                blend.onComplete = null;
                blend.events = null;
                blend.onEvent = null;
                blend.onFrame = null;
            }
        }


        public void Play(
            AnimzClip clip,
            float speed = 1.0f,
            AnimationCompleteDelegate onComplete = null,
            AnimationFrameDelegate onFrame = null,
            AnimationEventDelegate onEvent = null
            )
        {
            if (clip == null)
                return;

            var blendIndex = GetBlendIndex(clip.clip);
            if (-1 == blendIndex)
                return;

            StopAll(clip.blendTime);

            ref var blend = ref _blends[blendIndex];
            blend.events = clip.events;
            blend.duration = clip.blendTime;
            blend.elapsed = 0.0f;
            blend.weight = MinBlendWeight;
            blend.weightStart = MinBlendWeight;
            blend.weightEnd = MaxBlendWeight;
            blend.onComplete = blend.clip.isLooping ? null : onComplete;
            blend.onFrame = onFrame;
            blend.onEvent = onEvent;
            blend.eventTime = -1.0f;

            // Reset the time and speed of the clip
            blend.playable.Play();
            blend.playable.SetTime(0.0f);
            blend.playable.SetSpeed(speed * clip.speed);
            blend.playable.SetDone(false);

            if (!_playableGraph.IsPlaying())
                _playableGraph.Play();
        }

        public void Play (
            AnimationClip _clip, 
            float blendTime=DefaultBlendTime, 
            float speed = 1.0f, 
            AnimationCompleteDelegate onComplete = null,
            AnimationFrameDelegate onFrame = null)
        {
            var blendIndex = GetBlendIndex(_clip);
            if (-1 == blendIndex)
                return;

            StopAll(blendTime);

            ref var blend = ref _blends[blendIndex];
            blend.duration = blendTime;
            blend.elapsed = 0.0f;
            blend.weight = MinBlendWeight;
            blend.weightStart = MinBlendWeight;
            blend.weightEnd = MaxBlendWeight;
            blend.onComplete = blend.clip.isLooping ? null : onComplete;
            blend.onFrame = onFrame;
            blend.eventTime = -1.0f;

            // Reset the time and speed of the clip
            blend.playable.Play();
            blend.playable.SetTime(0.0f);
            blend.playable.SetSpeed(speed);
            blend.playable.SetDone(false);

            if (!_playableGraph.IsPlaying())
                _playableGraph.Play();
        }
        
        private int GetBlendIndex(AnimationClip clip) =>
            _clipInstanceIdToIndex.TryGetValue(clip.GetInstanceID(), out var index) ? index : -1;


        public void SetSpeed (AnimationClip clip, float speed)
        {
            var blendIndex = GetBlendIndex(clip);
            if (-1 == blendIndex)
                return;

            _blends[blendIndex].playable.SetSpeed(speed);
        }

        private void UpdateBlends(float deltaTime)
        {
            if (deltaTime <= 0.0f)
                return;

            // Update all of the blends
            var totalWeight = 0.0f;
            var totalPlaying = 0;
            for (int i = _blends.Length - 1; i >= 0; i--)
            {
                ref var blend = ref _blends[i];
                if (!blend.isPlaying)
                    continue;

                blend.elapsed = Mathf.Min(blend.elapsed + deltaTime, blend.duration);
                blend.weight = Mathf.Lerp(blend.weightStart, blend.weightEnd, blend.elapsed / blend.duration);

                if (blend.isBlendingOut && blend.weight <= MinBlendWeight)
                {
                    _mixer.SetInputWeight(i, 0.0f);
                    blend.onFrame = null;
                    blend.onEvent = null;
                    blend.events = null;
                    blend.playable.SetDone(true);
                    blend.playable.Pause();
                }
                else
                {
                    totalWeight += blend.weight;
                    totalPlaying++;
                }
            }

            // Manage the blender state 
            if (totalPlaying == 0)
                _playableGraph.Stop();

            // Update all blends using the total weight
            for (int i = _blends.Length - 1; i >= 0; i--)
            {
                ref var blend = ref _blends[i];
                if (blend.isPlaying)
                {
                    // Optional per frame callback
                    blend.onFrame?.Invoke(blend.normalizedTime);

                    // Play all events up to the blend's normalized time
                    PlayEvents(ref blend, blend.normalizedTime);

                    _mixer.SetInputWeight(i, blend.weight / totalWeight);
                }
            }

            // Issue the blend complete callbacks if any blends are finished
            for (int i=_blends.Length-1; i>=0; i--)
            {
                ref var blend = ref _blends[i];
                if (blend.isDone && blend.onComplete != null)
                {
                    var callback = blend.onComplete;
                    blend.onComplete = null;
                    callback(blend.clip);
                }
            }
        }

        private void PlayEvents (ref Blend blend, float normalizedTime)
        {
            if (blend.events == null)
                return;

            // If the normalized time is earlier than the last event time then assume we wrapped around and start over
            if (normalizedTime < blend.eventTime)
                blend.eventTime = -1.0f;

            for (int eventIndex = 0; eventIndex < blend.events.Length; eventIndex++)
            {
                ref var evt = ref blend.events[eventIndex];
                if (normalizedTime >= evt.time && blend.eventTime < evt.time)
                {
                    blend.onEvent?.Invoke(evt.raise);
                    onEvent?.Invoke(evt.raise);
                }
            }

            blend.eventTime = normalizedTime;
        }

        /// <summary>
        /// Allow the selected clip to be edited in the animation window
        /// </summary>
        void IAnimationClipSource.GetAnimationClips(List<AnimationClip> results)
        {
            if (null == _clips)
                return;

            foreach (var clip in _clips)
                results.Add(clip);
        }
    }
}
