using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace NoZ
{
    /// <summary>
    /// Base actor class that handles event propegation through child actor components.
    /// </summary>
    public class Actor : MonoBehaviour
    {
        [Flags]
        private enum Flags
        {
            SendUpdateEvent       = 1,
            SendFixedUpdateEvent  = 2
        }
        
        /// <summary>
        /// Pool used to allocate and reuse component lists as actors are created and destroyed
        /// </summary>
        private static readonly ObjectPool<List<ActorComponent>> componentsPool = 
            new ObjectPool<List<ActorComponent>>(() => new List<ActorComponent>(128), 128);

        /// <summary>
        /// Pool used to store lists of handlers
        /// </summary>
        private static readonly ObjectPool<List<ActorEventHandler>> handlersPool =
            new ObjectPool<List<ActorEventHandler>>(() => new List<ActorEventHandler>(128), 128);
        
        /// <summary>
        /// List of all components attached to the actor
        /// </summary>
        private List<ActorComponent> components = null;

        private List<ActorEventHandler> handlers = null;

        private Coroutine fixedUpdateCoroutine = null;
        
        private Coroutine updateCoroutine = null;

        private Flags flags = 0;

        public int componentCount => components?.Count ?? 0;

        public ActorComponent GetComponent(int index) => components[index];

        /// <summary>
        /// Return the component list back to the pool when the actor is destroy 
        /// </summary>
        protected virtual void OnDestroy()
        {
            if (components != null)
            {
                components.Clear();
                componentsPool.Release(components);
                components = null;
            }

            if (handlers != null)
            {
                handlers.Clear();
                handlersPool.Release(handlers);
                handlers = null;
            }
        }

        protected virtual void OnEnable()
        {
            if (null==updateCoroutine && IsUpdateCallbackRegistered)
                updateCoroutine = StartCoroutine(UpdateCoroutine());

            if (null == fixedUpdateCoroutine && IsFixedUpdateCallbackRegistered)
                fixedUpdateCoroutine = StartCoroutine(FixedUpdateCoroutine());
        }

        protected virtual void OnDisable()
        {
            if (updateCoroutine != null)
            {
                StopCoroutine(updateCoroutine);
                updateCoroutine = null;
            }

            if (fixedUpdateCoroutine != null)
            {
                StopCoroutine(fixedUpdateCoroutine);
                fixedUpdateCoroutine = null;
            }
        }

        /// <summary>
        /// Register the given actor component with the actor by adding it to
        /// its components list.
        /// </summary>
        /// <param name="component">Component to register</param>
        internal void RegisterComponent(ActorComponent component)
        {
            if (null == components)
                components = componentsPool.Get();

            components.Add(component);
            
            RegisterHandlers(component);
        }

        private void RegisterHandlers(ActorComponent component)
        {
            if (component.info.handlers.Length == 0)
                return;

            if (handlers == null)
                handlers = handlersPool.Get();

            // Increase the list capacity if needed
            if (handlers.Capacity < handlers.Count + component.info.handlers.Length)
                handlers.Capacity = handlers.Count + component.info.handlers.Length;
            
            // Insert each handler 
            foreach (var handler in component.info.handlers)
            {
                if (!handler.autoRegister)
                    continue;
                
                var temp = handler;
                temp.component = component;
                handlers.Insert(FindHandlerInsertionPoint(handler.priority), temp);
                OnCallbackRegistered(handler.eventType);
            }
        }

       
        internal void RegisterHandler(ActorEventHandler handler)
        {
            if (handlers == null)
                handlers = handlersPool.Get();

            handlers.Insert(FindHandlerInsertionPoint(handler.priority), handler);

            OnCallbackRegistered(handler.eventType);            
        }

        /// <summary>
        /// Find the insertion point of a handler within the handlers array
        /// </summary>
        /// <param name="priority">Priority of the handler being inserted</param>
        private int FindHandlerInsertionPoint(int priority)
        {
            var handlerIndex = 0;
            for (; handlerIndex < handlers.Count && handlers[handlerIndex].priority >= priority; handlerIndex++);
            return handlerIndex;
        }

        /// <summary>
        /// Unregister an actor component from the actor.  Generally this method
        /// is only used internally by ActorComponent to automatically unregister
        /// itself.
        /// </summary>
        /// <param name="component">Component to unregister</param>
        internal void UnregisterComponent(ActorComponent component)
        {
            if (handlers == null)
                return;

            // Remove all handlers from the registered handlers and record
            // all the the handlers that were unregistered.
            var unregistered = handlersPool.Get();
            for(int handlerIndex=handlers.Count-1; handlerIndex >= 0; handlerIndex--)
                if (handlers[handlerIndex].component == component)
                {
                    unregistered.Add(handlers[handlerIndex]);
                    handlers.RemoveAt(handlerIndex);
                }
            
            // Send out an event for each unregistered callback
            foreach(var handler in unregistered)
                OnCallbackUnregistered(handler.eventType);
            unregistered.Clear();
            handlersPool.Release(unregistered);

            if (handlers.Count <= 0)
            {
                handlersPool.Release(handlers);
                handlers = null;
            }
        }

        internal void UnregisterHandler(ActorEventHandler handler)
        {
            if (handlers == null)
                return;

            for(int handlerIndex=handlers.Count-1; handlerIndex >= 0; handlerIndex--)
                if (handlers[handlerIndex].component == handler.component && handlers[handlerIndex].eventType == handler.eventType)
                {
                    handlers.RemoveAt(handlerIndex);
                    break;
                }

            if (handlers.Count <= 0)
            {
                handlersPool.Release(handlers);
                handlers = null;
            }
            
            OnCallbackUnregistered(handler.eventType);
        }
        
        /// <summary>
        /// Send an event to the all registered actor components
        /// </summary>
        /// <param name="evt">Event to send</param>
        public void Send(ActorEvent evt)
        {
            if (null == handlers)
                return;

            evt.IsHandled = false;

            // Generate a temporary list of all event handlers for the given event.  This is 
            // done to allow the handler table to be modified during a send event
            var eventHandlers = handlersPool.Get();
            var eventType = evt.GetType();
            foreach(var handler in handlers)
                if(handler.eventType == eventType || handler.eventType == typeof(ActorEvent))
                    eventHandlers.Add(handler);

            // Send the envent to each of the matching event handlers.
            for (var handlerIndex = 0; !evt.IsHandled && handlerIndex < eventHandlers.Count; handlerIndex++)
            {
                var handler = eventHandlers[handlerIndex];
                if (handler.component != null && handler.component.isActiveAndEnabled)
                    handler.callback.Invoke(handler.component, evt);
            }

            eventHandlers.Clear();
            handlersPool.Release(eventHandlers);
        }

        /// <summary>
        /// Returns true if the given event type is handled by any enabled component 
        /// </summary>
        /// <param name="eventType">Type of event</param>
        /// <returns>True if the actor handles the event</returns>
        public bool HandlesEvent(Type eventType)
        {
            if (null == handlers)
                return false;
            
            foreach(var handler in handlers)
                if (handler.eventType == eventType)
                    return true;

            return false;
        }
        
        private readonly WaitForFixedUpdate fixedUpdateWait = new WaitForFixedUpdate();
        private readonly WaitForEndOfFrame updateWait = new WaitForEndOfFrame();

        private IEnumerator FixedUpdateCoroutine()
        {
            while (isActiveAndEnabled && IsFixedUpdateCallbackRegistered)
            {
                yield return fixedUpdateWait;
                Send(ActorEvent.Singleton<ActorFixedUpdateEvent>().Init(Time.fixedDeltaTime));
            }

            fixedUpdateCoroutine = null;
        }
        
        private IEnumerator UpdateCoroutine()
        {
            while (isActiveAndEnabled && IsUpdateCallbackRegistered)
            {
                yield return updateWait;
                Send(ActorEvent.Singleton<ActorUpdateEvent>().Init(Time.deltaTime));               
            }

            updateCoroutine = null;
        }
        
        /// <summary>
        /// Returns true if there are callbacks registered that required the update coroutine
        /// </summary>
        private bool IsUpdateCallbackRegistered => (flags & Flags.SendUpdateEvent) != 0; 

        /// <summary>
        /// Returns true if there are callbacks registered that require the fixed update coroutine
        /// </summary>
        private bool IsFixedUpdateCallbackRegistered => (flags & Flags.SendFixedUpdateEvent) != 0;

        /// <summary>
        /// Called when a callback is registered
        /// </summary>
        /// <param name="eventType">type of event being registered</param>
        protected virtual void OnCallbackRegistered(Type eventType)
        {
            if (eventType == typeof(ActorUpdateEvent))
                flags |= Flags.SendUpdateEvent;
            else if (eventType == typeof(ActorFixedUpdateEvent))
                flags |= Flags.SendFixedUpdateEvent;

            // Start the update coroutine if needed
            if (updateCoroutine == null && isActiveAndEnabled && IsUpdateCallbackRegistered)
                updateCoroutine = StartCoroutine(UpdateCoroutine());

            // Start the fixed coroutine if needed
            if (fixedUpdateCoroutine == null && isActiveAndEnabled && IsFixedUpdateCallbackRegistered)
                fixedUpdateCoroutine = StartCoroutine(FixedUpdateCoroutine());
            
            Send(ActorEvent.Singleton<CallbackRegisteredEvent>().Init(eventType));
        }
        
        /// <summary>
        /// Called when a callback for a given event type is unregistered
        /// </summary>
        /// <param name="eventType">type of event being unregistered</param>
        protected virtual void OnCallbackUnregistered(Type eventType)
        {
            if (eventType == typeof(ActorUpdateEvent))
                flags = HandlesEvent(eventType)?flags:(flags & ~Flags.SendUpdateEvent);
            else if (eventType == typeof(ActorFixedUpdateEvent))
                flags = HandlesEvent(eventType)?flags:(flags & ~Flags.SendFixedUpdateEvent);
            
            Send(ActorEvent.Singleton<CallbackUnregisteredEvent>().Init(eventType));
        }
        
        /// <summary>
        /// Generate collision event for entering a collision
        /// </summary>
        /// <param name="other"></param>
        private void OnCollisionEnter2D(Collision2D other)
        {
            Send(ActorEvent.Singleton<CollisionEnterEvent>().Init(other.gameObject.GetComponent<Actor>()));
        }

        /// <summary>
        /// Generate collision event for each frame a collision is still ocurring
        /// </summary>
        /// <param name="other"></param>
        private void OnCollisionStay2D(Collision2D other)
        {
            Send(ActorEvent.Singleton<CollisionStayEvent>().Init(other.gameObject.GetComponent<Actor>()));
        }
        
        /// <summary>
        /// Generate collision event when the collision finishes
        /// </summary>
        /// <param name="other"></param>
        private void OnCollisionExit2D(Collision2D other)
        {
            Send(ActorEvent.Singleton<CollisionExitEvent>().Init(other.gameObject.GetComponent<Actor>()));
        }
    }
}