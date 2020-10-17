using System;

namespace NoZ
{
    /// <summary>
    /// Event fired when a callback is unregistered on an actor
    /// </summary>
    public class CallbackUnregisteredEvent : ActorEvent
    {
        public Type eventType;

        public CallbackUnregisteredEvent Init(Type _eventType)
        {
            eventType = _eventType;
            return this;
        }

        public override string ToString() => $"{GetType()}: {eventType}";
    }
}