using System;
using System.Reflection;
using UnityEngine;

namespace NoZ
{
    [Serializable]
    public struct ActorEventType
    {
        [SerializeField] private string name;

        private Type type;

        public Type Type {
            get {
                if(null == type && name != null)
                    type = Type.GetType(name);

                return type;
            }
        }

        public static ActorEventType FromEvent(ActorEvent evt) => new ActorEventType { name = evt.GetType().FullName };

        public static ActorEventType FromType(Type evt)
        {
            if (!typeof(ActorEvent).IsAssignableFrom(evt))
                throw new ArgumentException("Type is not an ActorEvent");

            return new ActorEventType { name = evt.GetType().FullName };
        }

        public static ActorEventType FromType<T>() where T : ActorEvent => FromType(typeof(T));
    }

    public class ActorEvent
    {
        private static class SingletonWrapper<T> where T : ActorEvent, new()
        {
            public static readonly T Instance = new T();
        }

        /// <summary>
        /// Returns true if the event has already been handled.  This value is automatically set
        /// to false when an event it sent.
        /// </summary>
        public bool IsHandled { get; set; } = false;

        /// <summary>
        /// Returns a singleton instance of a give nevent
        /// </summary>
        /// <typeparam name="T">Event type</typeparam>
        /// <returns>Singleton instance</returns>
        public static T Singleton<T>() where T : ActorEvent , new() => SingletonWrapper<T>.Instance;
    }
    
      
    internal abstract class ActorEventDelegate
    {
        public abstract void Invoke (object target, ActorEvent evt);
        
        public static ActorEventDelegate Create(MethodInfo info)
        {
            var parameters = info.GetParameters();
            if(parameters.Length != 1)
                throw new ArgumentException("info");
            
            return (ActorEventDelegate)Activator.CreateInstance(
                typeof(ActorEventDelegateImpl<,>).MakeGenericType(info.DeclaringType,parameters[0].ParameterType), 
                info);
        }        
    }

    internal class ActorEventDelegateImpl<TTarget,TEvent> : ActorEventDelegate
        where TTarget : class
        where TEvent : ActorEvent
    {
        private delegate void InvokeDelegate (TTarget target, TEvent arg1);

        private readonly InvokeDelegate _invoke;

        public ActorEventDelegateImpl(MethodInfo methodInfo)
        {
            _invoke = (InvokeDelegate)Delegate.CreateDelegate(typeof(InvokeDelegate), methodInfo);
        }

        public ActorEventDelegateImpl(Action<ActorEvent> action)
        {
            _invoke = (InvokeDelegate)Delegate.CreateDelegate(typeof(InvokeDelegate), action.Method);
        }

        public override void Invoke (object target, ActorEvent evt) => _invoke((TTarget)target, (TEvent)evt);
    }
    
    internal struct ActorEventHandler
    {
        public ActorComponent component;
        public ActorEventDelegate callback;
        public Type eventType;
        public int priority;
        public bool autoRegister;
    }
    
    [AttributeUsage(AttributeTargets.Method)]
    public class ActorEventHandlerAttribute : Attribute
    {
        public int priority = 0;
        public bool autoRegister = true;
    }    
}
