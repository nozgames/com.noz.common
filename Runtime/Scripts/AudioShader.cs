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

        public AudioClip AudioClip = null;

        [Range(0, 1)]
        public float Volume = 1f;

        [Range(0, 2)]
        public float Pitch = 1f;

        public HapticFeedbackType HapticFeedback = HapticFeedbackType.None;

        public void Play()
        {
            AudioManager.Instance.Play(this);
        }

        public void Play(float volume, float pitch)
        {
            AudioManager.Instance.Play(this, volume, pitch);
        }
    }

}