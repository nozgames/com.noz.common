using UnityEngine;

namespace NoZ
{
    public class AudioManager : Singleton<AudioManager>
    {
        private struct ChannelInfo
        {
            public AudioSource source;
            public AudioChannel channel;

            public bool isPlaying => source.isPlaying;
        }

        private ChannelInfo[] _channels;

        protected override void OnInitialize()
        {
            base.OnInitialize();

            var go = new GameObject();
            go.transform.SetParent(gameObject.transform);
            go.name = "Sources";

            _channels = new ChannelInfo[20];
            for (int i = 0; i < _channels.Length; i++)
            {
                _channels[i].source = go.AddComponent<AudioSource>();
                _channels[i].source.playOnAwake = false;
            }
        }

        /// <summary>
        /// Play an audio clip
        /// </summary>
        /// <param name="clip">Audio clip</param>
        /// <param name="channel">Optional channel to play the clip on</param>
        public void Play(AudioClip clip, AudioChannel channel = null) => Play(clip, 1, 1);

        public void Play(AudioClip clip, float volume, float pitch, AudioChannel channel = null)
        {
            if (null == _channels)
                return;

            var channelIndex = -1;
            if(channel != null)
            {
                for (int i = 0; i < _channels.Length; i++)
                {
                    ref var channelRef = ref _channels[i];
                    if (channelRef.isPlaying && channelRef.channel == channel)
                    {
                        channelRef.source.Stop();
                        channelRef.channel = null;
                        channelIndex = i;
                        break;
                    }
                }
            }

            if(channelIndex == -1)
            {
                for (int i = 0; i < _channels.Length; i++)
                {
                    ref var channelRef = ref _channels[i];
                    if (channelRef.isPlaying)
                        continue;

                    channelIndex = i;
                    break;
                }
            }

            if (channelIndex == -1)
                return;

            var source = _channels[channelIndex].source;
            source.clip = clip;
            source.volume = volume * (channel?.volume ?? 1.0f);
            source.pitch = pitch * (channel?.pitch ?? 1.0f); ;
            source.Play();

            _channels[channelIndex].channel = channel;
        }

        public void Play(AudioShader shader) => Play(shader, 1f, 1f);

        public void Play(AudioShader shader, float volume, float pitch)
        {
            if (null == shader.clips)
                return;

            // Rando mclip from the list of clips
            var clip = shader.clips[Random.Range(0, shader.clips.Length - 1)];

            // Play the clip
            Play(clip, shader.volume * volume, shader.pitch * pitch, shader.channel);

            switch (shader.hapticFeedback)
            {
                case AudioShader.HapticFeedbackType.Selection:
                    HapticManager.Selection();
                    break;

                case AudioShader.HapticFeedbackType.NotificationError:
                    HapticManager.Notification(NotificationFeedback.Error);
                    break;

                case AudioShader.HapticFeedbackType.NotificationSuccess:
                    HapticManager.Notification(NotificationFeedback.Success);
                    break;

                case AudioShader.HapticFeedbackType.NotificationWarning:
                    HapticManager.Notification(NotificationFeedback.Warning);
                    break;

                case AudioShader.HapticFeedbackType.ImpactLight:
                    HapticManager.Impact(ImpactFeedback.Light);
                    break;

                case AudioShader.HapticFeedbackType.ImpactMedium:
                    HapticManager.Impact(ImpactFeedback.Medium);
                    break;

                case AudioShader.HapticFeedbackType.ImpactHeavy:
                    HapticManager.Impact(ImpactFeedback.Heavy);
                    break;
            }
        }
    }
}
