using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEditor.Animations;

namespace NoZ.UI
{
    public class SelectableStateProvider : StateProvider, ISelectHandler, IDeselectHandler
    {
        private static AnimatorController _animatorController = null;
        private static int _triggerPressed = -1;
        private static int _triggerSelected = -1;
        private static int _triggerDisabled = -1;
        private static int _triggerHover = -1;

        private Style.State _animationState = Style.State.Normal;
        private bool _selected = false;

        private static int AddAnimationControllerState (AnimatorController controller, AnimatorStateMachine stateMachine, string name)
        {
            controller.AddParameter(name, AnimatorControllerParameterType.Trigger);
            var state = stateMachine.AddState(name);
            var transition = stateMachine.AddAnyStateTransition(state);
            transition.AddCondition(AnimatorConditionMode.If, 1.0f, name);
            transition.duration = 0.0f;
            transition.hasFixedDuration = true;
            state.AddStateMachineBehaviour<StyleAnimatorState>();
            return Animator.StringToHash(name);
        }

        internal override void Attach (Component component)
        {
            var selectable = component as Selectable;
            if (null == selectable)
                throw new System.ArgumentException("component must be a Selectable");

            if(_animatorController == null)
            {
                var controller = new AnimatorController();
                controller.name = "UISelectableStateController";
                controller.AddLayer("Base");

                var stateMachine = controller.layers[0].stateMachine;
                AddAnimationControllerState(controller, stateMachine, "Normal");
                _triggerHover = AddAnimationControllerState(controller, stateMachine, "Hover");
                _triggerPressed = AddAnimationControllerState(controller, stateMachine, "Pressed");
                _triggerSelected = AddAnimationControllerState(controller, stateMachine, "Selected");
                _triggerDisabled = AddAnimationControllerState(controller, stateMachine, "Disabled");

                _animatorController = controller;
            }

            selectable.transition = Selectable.Transition.None;
            selectable.transition = Selectable.Transition.Animation;

            selectable.animationTriggers.normalTrigger = "Normal";
            selectable.animationTriggers.highlightedTrigger = "Hover";
            selectable.animationTriggers.pressedTrigger = "Pressed";
            selectable.animationTriggers.selectedTrigger = "Selected";
            selectable.animationTriggers.disabledTrigger = "Disabled";

            var animator = selectable.animator;
            if (null == selectable.animator)
                animator = selectable.gameObject.AddComponent<Animator>();

            animator.runtimeAnimatorController = _animatorController;

            foreach (var behavior in animator.GetBehaviours<StyleAnimatorState>())
                behavior.onStateEnter = OnAnimationState;
        }

        private void OnAnimationState(int hash)
        {
            _animationState = Style.State.Normal;

            if (hash == _triggerHover)
                _animationState = Style.State.Hover;
            else if (hash == _triggerPressed)
                _animationState = Style.State.Pressed;
            else if (hash == _triggerDisabled)
                _animationState = Style.State.Disabled;
            else if (hash == _triggerSelected)
                _animationState = Style.State.Selected;

            UpdateState();
        }

        public void OnDeselect(BaseEventData eventData)
        {
            _selected = false;
            UpdateState();
        }

        public void OnSelect(BaseEventData eventData)
        {
            _selected = true;
            UpdateState();
        }

        private void UpdateState()
        {
            if (_selected && _animationState == Style.State.Hover)
                state = Style.State.SelectedHover;
            else if (_selected && _animationState == Style.State.Pressed)
                state = Style.State.SelectedPressed;
            else
                state = _animationState;
        }
    }
}
