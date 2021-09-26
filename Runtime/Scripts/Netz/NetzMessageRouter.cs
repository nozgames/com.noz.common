#if UNITY_COLLECTIONS && UNITY_TRANSPORT

using System;
using System.Collections.Generic;
using Unity.Networking.Transport;

namespace NoZ.Netz 
{
    internal struct NetzMessageRouter<T> 
    {
        public delegate void Delegate(FourCC messageType, T target, ref DataStreamReader reader);

        private Dictionary<FourCC, Delegate> _routes;

        public void AddRoute (FourCC messageType, Delegate route)
        {
            if (null == _routes)
                _routes = new Dictionary<FourCC, Delegate>();

            if (_routes.ContainsKey(messageType))
                throw new InvalidOperationException("Multiple routes for same message identifier are not supported");

            _routes[messageType] = route;
        }

        public void RemoveRoute (FourCC messageType)
        {
            if (null == _routes)
                return;

            _routes.Remove(messageType);
        }

        public bool Route (FourCC messageType, T target, ref DataStreamReader reader)
        {
            if (null == _routes || !_routes.TryGetValue(messageType, out var route))
                return false;

            route(messageType, target, ref reader);

            return true;
        }
    }
}

#endif
