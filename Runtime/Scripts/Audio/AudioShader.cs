using UnityEngine;

namespace NoZ
{
    [CreateAssetMenu(fileName = "New Audio Shader", menuName = "NoZ/Audio Shader", order = 1)]
    public class AudioShader : ScriptableObject
    {
        public enum HapticFeedbackType
        {
            None,
            Selection,
            ImpactLight,
            ImpactMedium,
            ImpactHeavy,
            NotificationSuccess,
            NotificationWarning,
            NotificationError
        }

        public AudioClip[] clips = null;

        [Range(0, 1)]
        public float volume = 1f;

        [Range(0, 2)]
        public float pitch = 1f;

        public AudioChannel channel;

        public HapticFeedbackType hapticFeedback = HapticFeedbackType.None;

        public AudioClip GetRandomClip() => (clips == null || clips.Length == 0) ? null : clips[Random.Range(0, clips.Length - 1)];

        public void Play()
        {
            AudioManager.Instance?.Play(this);
        }

        public void Play(float volume, float pitch)
        {
            AudioManager.Instance?.Play(this, volume, pitch);
        }
    }

    public static class AudioSourceExtensions
    {
        public static void PlayOneShot(this AudioSource source, AudioShader shader)
        {
            var clip = shader.GetRandomClip();
            if (null == clip)
                return;

            source.volume = shader.volume;
            source.pitch = shader.pitch;
            source.PlayOneShot(clip);
        }
    }
}