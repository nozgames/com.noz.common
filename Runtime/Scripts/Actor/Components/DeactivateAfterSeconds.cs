using UnityEngine;

namespace NoZ.Rift
{
    public class DeactivateAfterSeconds : ActorComponent
    {
        /// <summary>
        /// Target to deactivate
        /// </summary>
        [SerializeField] private GameObject target = null;
        
        /// <summary>
        /// Amount of time to wait
        /// </summary>
        [SerializeField] private  float value = 1.0f;

        /// <summary>
        /// Amount of elapsed since the component was enabled 
        /// </summary>
        private float elapsed = 0.0f;

        private void Awake()
        {
            if (null == target)
                target = gameObject;
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            elapsed = 0.0f;
            RegisterHandler<ActorUpdateEvent>();
        }

        [ActorEventHandler(priority = -1, autoRegister = false)]
        private void OnActorUpdate(ActorUpdateEvent evt)
        {
            elapsed += Time.deltaTime;
            if (elapsed < value) 
                return;
            
            target.SetActive(false);

            // If we are still active after the call to setactive then
            // we need to stop the actor update.
            if (isActiveAndEnabled)
                UnregisterHandler<ActorUpdateEvent>();
        }        
    }
}