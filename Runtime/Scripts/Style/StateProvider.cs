using UnityEngine;

namespace NoZ.Style
{
    /// <summary>
    /// Abstract class used to define a class that privides state to the style system
    /// </summary>
    public abstract class StateProvider : MonoBehaviour
    {
        private Style.State _state = Style.State.Normal;

        public event System.Action<StateProvider> onStateChanged;

        public Style.State state
        {
            get => _state;
            protected set
            {
                if (_state == value)
                    return;

                _state = value;

                onStateChanged?.Invoke(this);
            }
        }

        protected internal abstract void Attach(Component component);
    }
}