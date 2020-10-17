using UnityEngine;

namespace NoZ
{
    public class PlayAudioClipOnEvent : ActorComponent
    {
        [SerializeField] private ActorEventType _event;
        [SerializeField] private AudioClip _clip = null; 

        protected override void OnEnable()
        {
            base.OnEnable();

            if (_event.Type == null || _clip == null)
                return;

            RegisterHandler(_event.Type, OnActorEvent);
        }

        private void OnActorEvent(ActorEvent evt)
        {
            if (evt.GetType() != _event.Type)
                return;

            AudioManager.Instance.Play(_clip);
        }
    }
}
