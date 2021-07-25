using UnityEngine;

namespace NoZ.UI
{
    public class UIStateController2 : MonoBehaviour
    {
        private UIStyle.State _state = UIStyle.State.Normal;

        public event System.Action<UIStateController2> onStateChanged;

        public UIStyle.State state
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

        public virtual void Attach (Component component)
        {
        }
    }
}