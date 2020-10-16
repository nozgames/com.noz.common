using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace NoZ
{
    public class AudioManager : MonoBehaviour
    {
        private static AudioManager _singleton = null;

        private AudioSource[] _sources = null;

        public static AudioManager Instance => _singleton;

        public AudioManager()
        {
            _singleton = this;
        }

        // Use this for initialization
        void Start()
        {
            var go = new GameObject();
            go.transform.SetParent(gameObject.transform);
            go.name = "Sources";

            _sources = new AudioSource[20];
            for (int i = 0; i < _sources.Length; i++)
            {
                _sources[i] = go.AddComponent<AudioSource>();
                _sources[i].playOnAwake = false;
            }
        }


        public void Play(AudioClip clip) => Play(clip, 1, 1);

        public void Play(AudioClip clip, float volume, float pitch)
        {
            foreach (var source in _singleton._sources)
            {
                if (source.isPlaying)
                    continue;

                source.clip = clip;
                source.volume = volume;
                source.pitch = pitch;
                source.Play();
                return;
            }
        }

        public void Play(AudioShader shader) => Play(shader, 1f, 1f);

        public void Play(AudioShader shader, float volume, float pitch)
        {
            if (null == shader.AudioClip)
                return;

            Play(shader.AudioClip, shader.Volume, shader.Pitch);

            switch (shader.HapticFeedback)
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
