using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace NoZ
{
    internal class ActorComponentInfo
    {
        public struct Dependency
        {
            public Type componentType;
            public FieldInfo field;
        }
        
        public ActorEventHandler[] handlers { get; private set; }
        public Dependency[] injection { get; private set; }

        public ActorComponentInfo(ActorEventHandler[] handlers_, Dependency[] dependencies)
        {
            handlers = handlers_;
            injection = dependencies;
        }
    }

    public class ActorDependencyAttribute : Attribute
    {
    }
    
    public class ActorComponent : MonoBehaviour
    {
        /// <summary>
        /// Dictionary of all actor component info per type
        /// </summary>
        private static readonly Dictionary<Type, ActorComponentInfo> infoByType = new Dictionary<Type, ActorComponentInfo>();

        private Actor _actor;

        /// <summary>
        /// Actor this handler is attached to.
        /// </summary>
        public Actor actor {
            get {
                if(_actor == null)
                    _actor = GetComponentInParent<Actor>();

                return _actor;
            }
        }

        internal ActorComponentInfo info { get; private set; }

        public bool HandlesEvent(Type eventType)
        {
            foreach(var handler in info.handlers)
                if (handler.eventType == eventType)
                    return true;

            return false;
        }

        internal bool TryGetHandler<T>(out ActorEventHandler result) => TryGetHandler(typeof(T), out result);

        internal bool TryGetHandler(Type eventType, out ActorEventHandler result)
        {
            foreach(var handler in info.handlers)
                if (handler.eventType == eventType)
                {
                    result = handler;
                    result.component = this;
                    return true;
                }

            result = new ActorEventHandler();
            return false;
        }

        protected void RegisterHandler(Type eventType, Action<ActorEvent> callback, int priority = 0)
        {
            if (!typeof(ActorEvent).IsAssignableFrom(eventType))
                throw new ArgumentException("type is not an ActorEvent");

            actor.RegisterHandler(new ActorEventHandler
            {
                autoRegister = false,
                callback = ActorEventDelegate.Create(callback.Method),
                component= this,
                eventType = eventType,
                priority = priority
            });
        }

        protected void UnregisterHandler (Type eventType, Action<ActorEvent> callback)
        {
            actor.UnregisterHandler(new ActorEventHandler
            {
                autoRegister = false,
                callback = ActorEventDelegate.Create(callback.Method),
                component = this,
                eventType = eventType,
                priority = 0
            });
        }

        protected void RegisterHandler<T>() where T : ActorEvent
        {
            // Find a handler for the event
            if (!TryGetHandler<T>(out var handler))
                return;

            actor.RegisterHandler(handler);
        }

        protected void UnregisterHandler<T>() where T : ActorEvent
        {
            if (!TryGetHandler<T>(out var handler))
                return;

            handler.component = this;
            actor.UnregisterHandler(handler);
        }

        public T GetActorComponent<T>() where T : ActorComponent => GetActorComponent(typeof(T)) as T;

        public ActorComponent GetActorComponent(Type type) 
        {
            if (null == actor)
                return null;

            if (!typeof(ActorComponent).IsAssignableFrom(type))
                return null;

            return actor.GetComponentInChildren(type) as ActorComponent;
        }
        
        private static readonly List<ActorEventHandler> tempHandlersList = new List<ActorEventHandler>(16);
        private static readonly List<ActorComponentInfo.Dependency> tempDependencyList = new List<ActorComponentInfo.Dependency>(16);

        protected virtual void OnEnable()
        {
            // Lazy create the handlers array for this handler when first enabled
            var type = GetType();
            if (!infoByType.TryGetValue(type, out var existingInfo))
            {
                tempHandlersList.Clear();
                tempDependencyList.Clear();
                for (var t = type; t != null && t != typeof(ActorComponent); t = t.BaseType)
                {
                    var methods = t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    foreach (var method in methods)
                    {
                        var attrs = method.GetCustomAttributes(typeof(ActorEventHandlerAttribute), true);
                        if (attrs.Length == 0)
                            continue;
                        
                        var attr = attrs[0] as ActorEventHandlerAttribute;
                        if (null == attr)
                            continue;

                        var parameters = method.GetParameters();
                        if (parameters.Length != 1)
                        {
                            Debug.LogError($"{t.Name}.{method.Name}: ActorEvent callbacks must take a single parameter derived from ActorEvent");
                            continue;
                        }

                        var eventType = parameters[0].ParameterType;
                        if (!typeof(ActorEvent).IsAssignableFrom(eventType))
                        {
                            Debug.LogError($"{t.Name}.{method.Name}: ActorEvent callbacks must take a single parameter derived from ActorEvent");
                            continue;
                        }
                        
                        tempHandlersList.Add(new ActorEventHandler
                        {
                            priority = attr.priority, 
                            autoRegister = attr.autoRegister, 
                            callback = ActorEventDelegate.Create(method), 
                            eventType = eventType
                        });
                    }
                    
                    var fields = t.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    foreach (var field in fields)
                    {
                        var attrs = field.GetCustomAttributes(typeof(ActorDependencyAttribute), true);
                        if (attrs.Length == 0)
                            continue;
                        
                        var attr = attrs[0] as ActorDependencyAttribute;
                        if (null == attr)
                            continue;

                        tempDependencyList.Add(new ActorComponentInfo.Dependency
                        {
                            field = field,
                            componentType = field.FieldType
                        });
                    }
                }
               
                infoByType[type] = info = new ActorComponentInfo(tempHandlersList.ToArray(), tempDependencyList.ToArray());
                tempHandlersList.Clear();
            }
            else
                info = existingInfo;

            actor.RegisterComponent(this);
            
            foreach(var dependency in info.injection)
                dependency.field.SetValue(this, GetActorComponent(dependency.componentType));
        }

        protected virtual void OnDisable()
        {
            if (null == actor)
                return;
            
            actor.UnregisterComponent(this);
            
            // Send out a callback unregistered event for each callback that was registered
            foreach(var handler in info.handlers)
                actor.Send(ActorEvent.Singleton<CallbackUnregisteredEvent>().Init(handler.eventType));
        }

        /// <summary>
        /// Call to handle an event by callin the handlers callback if available
        /// </summary>
        /// <param name="evt">Event to handle</param>
        internal void HandleEvent(ActorEvent evt)
        {
            var eventType = evt.GetType();
            foreach(var handler in info.handlers)
                if (handler.eventType == eventType)
                    handler.callback.Invoke(this, evt);
        }

        /// <summary>
        /// Send an event to all handlers on the parent actor
        /// </summary>
        protected void Send(ActorEvent evt)
        {
            if (actor != null)
                actor.Send(evt);
        }
    }
}