namespace NoZ.Events
{
    /// <summary>
    /// Delegate called when a GameEvent is raised
    /// </summary>
    /// <typeparam name="T">Type of event</typeparam>
    /// <param name="sender">Object that sent the event</param>
    /// <param name="evt">Event data</param>
    public delegate void GameEventDelegate<T>(object sender, T evt);

    /// <summary>
    /// Non generic implementation of GameEvent that provides an eaiser way to raise events
    /// </summary>
    public class GameEvent
    {
        /// <summary>
        /// Raise an event of type <typeparamref name="TEvent"/>
        /// </summary>
        /// <typeparam name="TEvent">Event type</typeparam>
        /// <param name="sender">Object that sent the event</param>
        /// <param name="evt">Event data</param>
        public static void Raise<TEvent>(object sender, TEvent evt) => GameEvent<TEvent>.Raise(sender, evt);
    }

    /// <summary>
    /// Container for all event delegates for a given event type
    /// </summary>
    /// <typeparam name="TEvent">Event type</typeparam>
    public class GameEvent<TEvent>
    {
        /// <summary>
        /// Event delegates that are called when the GameEvent is raised
        /// </summary>
        public static event GameEventDelegate<TEvent> OnRaised;

        /// <summary>
        /// Raise an event of type <typeparamref name="TEvent"/>
        /// </summary>
        /// <param name="sender">Object that sent the event</param>
        /// <param name="evt">Event data</param>
        public static void Raise(object sender, TEvent evt) => OnRaised?.Invoke(sender, evt);
    }
}
