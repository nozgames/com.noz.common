using UnityEngine;

namespace NoZ.Rift
{
    public class DestroyActorOnCollision : ActorComponent
    {
        [ActorEventHandler(priority = -1)]
        private void OnCollision (CollisionEnterEvent evt)
        {
            Destroy(actor.gameObject);
        }
    }
}