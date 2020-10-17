using UnityEngine;

namespace NoZ
{
    /// <summary>
    /// Add to actor to enable logging of all events sent to the actor
    /// </summary>
    public class ActorEventLogger : ActorComponent
    {
        public bool showUpdate = false;
        public bool showFixedUpdate = false;

        [ActorEventHandler]
        private void OnActorEvent (ActorEvent evt)
        {
            if (!showUpdate && evt.GetType() == typeof(ActorUpdateEvent)) return;
            if (!showFixedUpdate && evt.GetType() == typeof(ActorFixedUpdateEvent)) return;

            Debug.Log($"{actor.name}: {evt}");
        }
    }
}
