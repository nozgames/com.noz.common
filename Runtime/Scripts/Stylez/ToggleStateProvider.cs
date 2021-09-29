using System;
using UnityEngine;
using UnityEngine.UI;

#if false
namespace NoZ.Stylez
{
#if UNITY_EDITOR
    public class ToggleStateProvider : SelectableStateProvider
    {
        protected internal override void Attach(Component component)
        {
            base.Attach(component);
            (selectable as Toggle).onValueChanged.AddListener(OnValueChanged);
        }

        protected override void OnDisable()
        {
            (selectable as Toggle).onValueChanged.RemoveListener(OnValueChanged);
        }

        private void OnValueChanged(bool arg0)
        {
            UpdateState();
        }

        protected override StylezStyle.State GetCurrentState()
        {
            var toggle = selectable as Toggle;
            var state = base.GetCurrentState();

            if(toggle.isOn && toggle.group != null)
            {
                switch(state)
                {
                    case StylezStyle.State.Pressed: return StylezStyle.State.Normal;
                    case StylezStyle.State.Hover: return StylezStyle.State.Pressed;
                    case StylezStyle.State.Selected: return StylezStyle.State.SelectedPressed;
                    case StylezStyle.State.SelectedHover: return StylezStyle.State.SelectedPressed;
                    case StylezStyle.State.Normal: return StylezStyle.State.Pressed;
                }
            }

            return state;
        }
    }
#endif
}

#endif
