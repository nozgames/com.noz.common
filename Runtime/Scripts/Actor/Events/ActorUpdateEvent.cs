namespace NoZ
{
    public class ActorUpdateEvent : ActorEvent
    {
        public float deltaTime { get; private set; }

        public ActorUpdateEvent Init(float deltaTime)
        {
            this.deltaTime = deltaTime;
            return this;
        }
    }

    public class ActorFixedUpdateEvent : ActorEvent
    {
        public float deltaTime { get; private set; }

        public ActorFixedUpdateEvent Init(float deltaTime)
        {
            this.deltaTime = deltaTime;
            return this;
        }
    }    
}