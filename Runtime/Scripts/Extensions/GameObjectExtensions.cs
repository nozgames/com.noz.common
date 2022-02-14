using UnityEngine;

namespace NoZ
{
    public static class GameObjectExtensions
    {
        /// <summary>
        /// Get a component of type <typeparamref name="TComponent"/> and if none was found add one instead.
        /// </summary>
        /// <typeparam name="TComponent">Component type to get or add</typeparam>
        /// <param name="gameObject">GameObject to get component from or add component to</param>
        /// <returns>The requested component</returns>
        public static TComponent GetOrAddComponent<TComponent> (this GameObject gameObject) where TComponent : Component
        {
            if(!gameObject.TryGetComponent<TComponent>(out var r))
                r = gameObject.gameObject.AddComponent<TComponent>();

            return r;
        }
    }
}
