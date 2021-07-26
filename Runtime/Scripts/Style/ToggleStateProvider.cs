using System;
using UnityEngine;
using UnityEngine.UI;

namespace NoZ.Style
{
    public class ToggleStateProvider : SelectableStateProvider
    {
        internal override void Attach(Component component)
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

        protected override Style.State GetCurrentState()
        {
            var toggle = selectable as Toggle;
            var state = base.GetCurrentState();

            if(toggle.isOn && toggle.group != null)
            {
                switch(state)
                {
                    case Style.State.Pressed: return Style.State.Normal;
                    case Style.State.Hover: return Style.State.Pressed;
                    case Style.State.Selected: return Style.State.SelectedPressed;
                    case Style.State.SelectedHover: return Style.State.SelectedPressed;
                    case Style.State.Normal: return Style.State.Pressed;
                }
            }

            return state;
        }
    }
}
