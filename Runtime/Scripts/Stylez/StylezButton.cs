using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System;

namespace NoZ.Stylez
{
    [RequireComponent(typeof(StylezStyle))]
    public class StylezButton : Button, IStylezStateProvider
    {
        private Action<StylezState> _stateChangedCallback;
        private bool _selected;

        public StylezState GetState()
        {
            var state = StylezState.Disabled;
            if (interactable && currentSelectionState != SelectionState.Disabled)
                state = currentSelectionState switch
                {
                    SelectionState.Normal => StylezState.Normal,
                    SelectionState.Highlighted => _selected ? StylezState.SelectedHover : StylezState.Hover,
                    SelectionState.Pressed => _selected ? StylezState.SelectedPressed : StylezState.Pressed,
                    SelectionState.Selected => StylezState.Selected,
                    _ => StylezState.Normal
                };

            return state;
        }

        public void SetStateChangedCallback(Action<StylezState> callback) => _stateChangedCallback = callback;

        protected override void Awake()
        {
            // Stylez buttons should never have a transition
            transition = Transition.None;

            base.Awake();
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
            _stateChangedCallback?.Invoke(GetState());
        }

        public override void OnDeselect (BaseEventData eventData)
        {
            _selected = false;
            base.OnDeselect(eventData);
            _stateChangedCallback?.Invoke(GetState());
        }
    }
}

