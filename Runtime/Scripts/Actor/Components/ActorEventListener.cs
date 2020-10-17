using UnityEngine;

namespace NoZ.Rift
{
    public class ActorEventListener : ActorComponent
    {
        public Actor _target = null;

        public Actor target
        {
            get => _target;
            set
            {
                if (_target == value)
                    return;

                Unregister();
                _target = value;
                
                if(isActiveAndEnabled)
                    Register();
            }
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            Register();
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            Unregister();
        }

        private void Register()
        {
            if (target == null)
                return;
            
            if(TryGetHandler<ActorEvent>(out var handler))
                target.RegisterHandler(handler);
        }
        
        private void Unregister()
        {
            if (target == null)
                return;

            if(TryGetHandler<ActorEvent>(out var handler))
                target.UnregisterHandler(handler);
        }

        [ActorEventHandler(autoRegister=false)]
        private void OnActorEvent(ActorEvent evt)
        {
            Send(evt);
        }
    }
}