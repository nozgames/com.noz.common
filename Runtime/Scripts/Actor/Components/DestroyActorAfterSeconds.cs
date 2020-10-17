using UnityEngine;

namespace NoZ.Rift
{
    public class DestroyActorAfterSeconds : ActorComponent
    {
        [SerializeField] private float seconds = 1.0f;
        
        [ActorEventHandler]
        private void OnActorUpdate(ActorUpdateEvent evt)
        {
            seconds -= Time.deltaTime;
            if (seconds <= 0.0f)
                Destroy(actor.gameObject);
        }
    }
}