using UnityEngine;

namespace NoZ
{
    /// <summary>
    /// Activate a target game object when the parent actor receives an event of the given type
    /// </summary>
    public class ActivateOnEvent : ActorComponent
    {
        [SerializeField] private ActorEventType _event;
        [SerializeField] private GameObject target = null;

        protected override void OnEnable()
        {
            base.OnEnable();

            if (_event.Type == null || target == null)
                return;

            RegisterHandler(_event.Type, OnActorEvent);
        }

        protected override void OnDisable()
        {
            base.OnDisable();

            if (_event.Type == null || target == null)
                return;

            UnregisterHandler(_event.Type, OnActorEvent);
        }

        private void OnActorEvent(ActorEvent evt)
        {
            if(target != null)
                target.gameObject.SetActive(true);
        }
    }
}
