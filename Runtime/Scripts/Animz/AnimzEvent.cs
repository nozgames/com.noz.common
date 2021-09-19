using UnityEngine;

namespace NoZ.Animz
{
    [CreateAssetMenu(menuName = "NoZ/Animz/Event")]
    public class AnimzEvent : ScriptableObject
    {
        private int _hash = 0;

        public void OnEnable()
        {
            _hash = Animator.StringToHash(name);
        }
    }
}
