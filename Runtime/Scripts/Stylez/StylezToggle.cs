using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System;

namespace NoZ.Stylez
{
    [RequireComponent(typeof(StylezToggle))]
    public class StylezToggle : Toggle, IStylezStateProvider
    {
        private Action<StylezState> _stateChangedCallback;
        private bool _selected;

        public StylezState GetState()
        {
            if (!interactable || currentSelectionState == SelectionState.Disabled)
                return StylezState.Disabled;

            var state = currentSelectionState switch
            {
                SelectionState.Normal => StylezState.Normal,
                SelectionState.Highlighted => _selected ? StylezState.SelectedHover : StylezState.Hover,
                SelectionState.Pressed => _selected ? StylezState.SelectedPressed : StylezState.Pressed,
                SelectionState.Selected => StylezState.Selected,
                _ => StylezState.Normal
            };

            if (isOn && group != null)
            {
                state = state switch
                {
                    StylezState.Pressed => StylezState.Normal,
                    StylezState.Hover => StylezState.Pressed,
                    StylezState.Selected => StylezState.SelectedPressed,
                    StylezState.SelectedHover => StylezState.SelectedPressed,
                    StylezState.Normal => StylezState.Pressed,
                    _ => state
                };
            }
            else if (isOn)
                state = StylezState.Pressed;

            return state;
        }

        public void SetStateChangedCallback(Action<StylezState> callback) => _stateChangedCallback = callback;

        protected override void Awake()
        {
            // Stylez buttons should never have a transition
            transition = Transition.None;
            onValueChanged.AddListener(OnValueChanged);

            base.Awake();
        }

        private void OnValueChanged (bool value)
        {
            _stateChangedCallback?.Invoke(GetState());
        }

        protected override void DoStateTransition(SelectionState state, bool instant)
        {
            base.DoStateTransition(state, instant);
            _stateChangedCallback?.Invoke(GetState());
        }            

        public override void OnSelect(BaseEventData eventData)
        {
            _selected = true;
            base.OnSelect(eventData);
        }

        public override void OnDeselect (BaseEventData eventData)
        {
            _selected = false;
            base.OnDeselect(eventData);
        }
    }
}

