namespace NoZ
{
    public class CollisionEnterEvent : ActorEvent
    {
        public Actor other { get; private set; }

        public CollisionEnterEvent Init(Actor other)
        {
            this.other = other;
            return this;
        }
    }
 
    public class CollisionStayEvent : ActorEvent
    {
        public Actor other { get; private set; }

        public CollisionStayEvent Init(Actor other)
        {
            this.other = other;
            return this;
        }
    }

    public class CollisionExitEvent : ActorEvent
    {
        public Actor other { get; private set; }

        public CollisionExitEvent Init(Actor other)
        {
            this.other = other;
            return this;
        }
    }

}