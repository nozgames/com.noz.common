#if UNITY_COLLECTIONS && UNITY_TRANSPORT

using System;
using System.Collections.Generic;
using Unity.Networking.Transport;

namespace NoZ.Netz 
{
    internal struct NetzMessageRouter<T> 
    {
        public delegate void Delegate(FourCC messageId, T target, ref DataStreamReader reader);

        private Dictionary<FourCC, Delegate> _routes;

        public void AddRoute (FourCC messageId, Delegate route)
        {
            if (null == _routes)
                _routes = new Dictionary<FourCC, Delegate>();

            if (_routes.ContainsKey(messageId))
                throw new InvalidOperationException("Multiple routes for same message identifier are not supported");

            _routes[messageId] = route;
        }

        public void RemoveRoute (FourCC messageId)
        {
            if (null == _routes)
                return;

            _routes.Remove(messageId);
        }

        public bool Route (FourCC messageId, T target, ref DataStreamReader reader)
        {
            if (null == _routes || !_routes.TryGetValue(messageId, out var route))
                return false;

            route(messageId, target, ref reader);

            return true;
        }
    }
}

#endif
