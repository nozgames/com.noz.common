using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Animations;
using System.Collections.Generic;

namespace NoZ.Animz
{
    public class AnimzBlendedController : MonoBehaviour
    {
        private const int MaxBlendPoolSize = 64;
        private const float DefaultBlendTime = 0.1f;
        private const float MinBlendWeight = 0.0001f;
        private const float MaxBlendWeight = 1.0f;

        public delegate void AnimationCompleteDelegate();
        public delegate void AnimationFrameDelegate (float normalizedTime);
        public delegate void AnimationEventDelegate(AnimzEvent evt);
       
        private abstract class Blend
        {
            public int index;
            public float elapsed;
            public float duration;
            public float weight;
            public float weightStart;
            public float weightEnd;
            public double eventTime;
            public AnimationCompleteDelegate onComplete;
            public AnimationFrameDelegate onFrame;
            public AnimationEventDelegate onEvent;

            public abstract AnimzClip.AnimzEventFrame[] events { get; }

            public abstract bool isLooping { get; }
            public abstract bool isDone { get; }
            public abstract bool isPlaying { get; }
            public abstract float normalizedTime { get; }

            public bool isBlendingOut => weightEnd < MaxBlendWeight;
            public bool isBlendingIn => !isBlendingOut;

            public abstract void Play(float speed);
            public abstract void Pause();
            public abstract void SetSpeed(float speed);
        }

        private class ClipBlend : Blend
        {
            public AnimzClip clip;            
            public AnimationClipPlayable playable;
            public override AnimzClip.AnimzEventFrame[] events => clip.events;
            public override bool isLooping => clip.isLooping;
            public override bool isDone => playable.IsDone();
            public override bool isPlaying => playable.GetPlayState() == PlayState.Playing;
            public override float normalizedTime =>
                isLooping ?
                    (float)((playable.GetTime() % clip.length) / clip.length) :
                    Mathf.Min((float)(playable.GetTime() / clip.length), 1.0f);

            public override void Play (float speed)
            {
                playable.Play();
                playable.SetTime(0.0f);
                playable.SetSpeed(speed * clip.speed);
                playable.SetDone(false);
            }

            public override void Pause()
            {
                playable.SetDone(true);
                playable.Pause();
            }

            public override void SetSpeed(float speed) => playable.SetSpeed(speed * clip.speed);
        }

        private class BlendTreeBlend : Blend
        {
            public AnimzBlendTree blendTree;
            public AnimationMixerPlayable mixer;
            public override AnimzClip.AnimzEventFrame[] events => blendTree.events;
            public override bool isLooping => blendTree.isLooping;
            public override bool isDone => mixer.IsDone();
            public override bool isPlaying => mixer.GetPlayState() == PlayState.Playing;
            public override float normalizedTime =>
                isLooping ?
                    (float)((mixer.GetTime() % blendTree.length) / blendTree.length) :
                    Mathf.Min((float)(mixer.GetTime() / blendTree.length), 1.0f);

            public override void Play(float speed)
            {
                mixer.Play();
                mixer.SetTime(0.0f);
                mixer.SetSpeed(speed * blendTree.speed);
                mixer.SetDone(false);
            }

            public override void Pause()
            {
                mixer.SetDone(true);
                mixer.Pause();
            }

            public override void SetSpeed(float speed) => mixer.SetSpeed(speed * blendTree.speed);
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

        private static List<ClipBlend> _clipBlendPool = new List<ClipBlend>(MaxBlendPoolSize);
        private static List<BlendTreeBlend> _blendTreePool = new List<BlendTreeBlend>(MaxBlendPoolSize);

        [SerializeField] private Animator _animator = null;

        private PlayableGraph _playableGraph;
        private AnimationPlayableOutput _playableOutput;
        private AnimationMixerPlayable _mixer;
        private ScriptPlayable<Blender> _blender;
        private Dictionary<int, Blend> _blendMap;
        private List<Blend> _blends = null;

        /// <summary>
        /// Event that is raised for any animation event raised on any playing clip
        /// </summary>
        public event AnimationEventDelegate onEvent = null;

        private void Awake()
        {
            if (null == _animator)
                _animator = GetComponent<Animator>();

            InitializeGraph();
        }

        private void InitializeGraph ()
        {
            if (_playableGraph.IsValid())
                return;

            _playableGraph = PlayableGraph.Create();
            _playableGraph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);

            _blender = ScriptPlayable<Blender>.Create(_playableGraph, 1);
            _blender.GetBehaviour()._controller = this;

            _mixer = AnimationMixerPlayable.Create(_playableGraph, 0);

            _playableGraph.Connect(_mixer, 0, _blender, 0);

            _playableOutput = AnimationPlayableOutput.Create(_playableGraph, "Animation", _animator);
            _playableOutput.SetSourcePlayable(_blender);

            _blendMap = new Dictionary<int, Blend>();
            _blends = new List<Blend>();
        }

        private void OnEnable()
        {
            for (int i=_blends.Count-1; i>=0; i--)
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
            // Release all of the blends back to the pool
            for (int i = _blends.Count - 1; i >= 0; i--)
                DestroyBlend(_blends[i]);
            
            _blends.Clear();

            _playableGraph.Destroy();
        }

        public void StopAll (float blendTime = DefaultBlendTime)
        {
            if(blendTime <= 0.0f)
            {
                _blends.Clear();
                return;
            }

            for (int i = _blends.Count - 1; i >= 0; i--)
            {
                var blend = _blends[i];
                if (blend.isBlendingOut)
                    continue;

                blend.duration = blendTime;
                blend.elapsed = 0.0f;
                blend.weightStart = blend.weight;
                blend.weightEnd = MinBlendWeight;
                blend.onComplete = null;
                blend.onEvent = null;
                blend.onFrame = null;
            }
        }

        public void Play(AnimzBlendTree blendTree, float speed = 1.0f,
            AnimationCompleteDelegate onComplete = null,
            AnimationFrameDelegate onFrame = null,
            AnimationEventDelegate onEvent = null)
        {
            // TODO: we will need a single blend that describes the blend tree
            // TODO: the blend tree itself will be a mixer
            // TODO: the blend value of the blend tree will determine the mixer weights (lerp between two closest positions)
            // TODO: all clips in the blend tree must play together 
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

            var blend = GetBlend(clip);
            if(null == blend)
                return;
                
            StopAll(clip.blendTime);

            blend.duration = clip.blendTime;
            blend.elapsed = 0.0f;
            blend.weight = MinBlendWeight;
            blend.weightStart = MinBlendWeight;
            blend.weightEnd = MaxBlendWeight;
            blend.onComplete = blend.isLooping ? null : onComplete;
            blend.onFrame = onFrame;
            blend.onEvent = onEvent;
            blend.eventTime = -1.0f;

            // Reset the time and speed of the clip
            blend.Play(speed);

            if (!_playableGraph.IsPlaying())
                _playableGraph.Play();
        }

        private Blend GetBlend(AnimzClip clip)
        {
            if (!_playableGraph.IsValid())
                InitializeGraph();

            if (_blendMap.TryGetValue(clip.GetInstanceID(), out var existingBlend))
                return existingBlend;

            ClipBlend blend;
            if (_clipBlendPool.Count > 0)
            {
                blend = _clipBlendPool[_clipBlendPool.Count - 1];
                _clipBlendPool.RemoveAt(_clipBlendPool.Count - 1);
            }
            else
                blend = new ClipBlend();

            blend.index = _blends.Count;
            blend.clip = clip;
            blend.playable = AnimationClipPlayable.Create(_playableGraph, clip.clip);

            AddBlend(blend, clip.GetInstanceID());

            _playableGraph.Connect(blend.playable, 0, _mixer, blend.index);

            if (!clip.isLooping)
            {
                blend.playable.SetDuration(clip.length);
                blend.playable.SetDone(true);
            }

            return blend;
        }

        private Blend GetBlend(AnimzBlendTree blendTree)
        {
            return null;
        }

        /// <summary>
        /// Set the playback speed for the given animation clip
        /// </summary>
        /// <param name="clip">Animation clip</param>
        /// <param name="speed">Speed multiplier</param>
        public void SetSpeed(AnimzClip clip, float speed) => GetBlend(clip)?.SetSpeed(speed);

        private void UpdateBlends(float deltaTime)
        {
            if (deltaTime <= 0.0f)
                return;

            // Update all of the blends
            var totalWeight = 0.0f;
            var totalPlaying = 0;
            for (int i = _blends.Count - 1; i >= 0; i--)
            {
                var blend = _blends[i];
                if (!blend.isPlaying)
                    continue;

                blend.elapsed = Mathf.Min(blend.elapsed + deltaTime, blend.duration);
                blend.weight = Mathf.Lerp(blend.weightStart, blend.weightEnd, blend.elapsed / blend.duration);

                if (blend.isBlendingOut && blend.weight <= MinBlendWeight)
                {
                    _mixer.SetInputWeight(i, 0.0f);
                    blend.onFrame = null;
                    blend.onEvent = null;
                    blend.Pause();
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
            for (int i = _blends.Count - 1; i >= 0; i--)
            {
                var blend = _blends[i];
                if (blend.isPlaying)
                {
                    // Optional per frame callback
                    blend.onFrame?.Invoke(blend.normalizedTime);

                    // Play all events up to the blend's normalized time
                    PlayEvents(blend, blend.normalizedTime);

                    _mixer.SetInputWeight(i, blend.weight / totalWeight);
                }
            }

            // Issue the blend complete callbacks if any blends are finished
            for (int i=_blends.Count-1; i>=0; i--)
            {
                var blend = _blends[i];
                if (blend.isDone && blend.onComplete != null)
                {
                    PlayEvents(blend, 1.0f);

                    var callback = blend.onComplete;
                    blend.onComplete = null;
                    callback();
                }
            }
        }

        private void PlayEvents (Blend blend, float normalizedTime)
        {
            if (blend.events == null)
                return;

            if (normalizedTime == blend.eventTime)
                return;

            // If the normalized time is earlier than the last event time then assume we wrapped around and start over
            if (normalizedTime < blend.eventTime)
            {
                // First play all events up to the end of the clip
                PlayEvents(blend, 1.0f);

                blend.eventTime = -1.0f;
            }

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
        /// Release the given blend back to the blend pool
        /// </summary>
        private void DestroyBlend (Blend blend)
        {
            if(blend is BlendTreeBlend blendTreeBlend)
            {
                if (_blendTreePool.Count < MaxBlendPoolSize)
                    _blendTreePool.Add(blendTreeBlend);
                return;
            }

            if (blend is ClipBlend clipBlend)
            {
                if (_clipBlendPool.Count < MaxBlendPoolSize)
                    _clipBlendPool.Add(clipBlend);
                return;
            }
        }

        /// <summary>
        /// Create a new blend using the given animation clip
        /// </summary>
        private Blend CreateBlend (AnimzClip clip)
        {
            ClipBlend blend;
            if (_clipBlendPool.Count > 0)
            {
                blend = _clipBlendPool[_clipBlendPool.Count - 1];
                _clipBlendPool.RemoveAt(_clipBlendPool.Count - 1);
            }
            else
                blend = new ClipBlend();

            blend.index = _blends.Count;
            blend.clip = clip;
            blend.playable = AnimationClipPlayable.Create(_playableGraph, clip.clip);

            _playableGraph.Connect(blend.playable, 0, _mixer, blend.index);

            if (!clip.isLooping)
            {
                blend.playable.SetDuration(clip.length);
                blend.playable.SetDone(true);
            }

            return blend;
        }

        private void AddBlend (Blend blend, int id)
        {
            _blendMap[id] = blend;
            _blends.Add(blend);

            _mixer.SetInputCount(blend.index + 1);
            
            _mixer.SetInputWeight(blend.index, 0.0f);
            blend.Pause();
        }
    }
}
