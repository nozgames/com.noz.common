using System;

namespace NoZ
{
    /// <summary>
    /// Event fired when a callback is registered on an actor
    /// </summary>
    public class CallbackRegisteredEvent : ActorEvent
    {
        public Type eventType;

        public CallbackRegisteredEvent Init(Type _eventType)
        {
            eventType = _eventType;
            return this;
        }

        public override string ToString() => $"{GetType()}: {eventType}";
    }
}