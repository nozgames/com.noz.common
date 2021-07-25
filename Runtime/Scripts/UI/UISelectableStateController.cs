using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEditor.Animations;

namespace NoZ.UI
{
    public class UISelectableStateController : UIStateController2, ISelectHandler, IDeselectHandler
    {
        private static AnimatorController _animatorController = null;
        private static int _triggerNormal = -1;
        private static int _triggerPressed = -1;
        private static int _triggerSelected = -1;
        private static int _triggerDisabled = -1;
        private static int _triggerHover = -1;

        private UIStyle.State _animationState = UIStyle.State.Normal;
        private bool _selected = false;

        private static int AddAnimationControllerState (AnimatorController controller, AnimatorStateMachine stateMachine, string name)
        {
            controller.AddParameter(name, AnimatorControllerParameterType.Trigger);
            var state = stateMachine.AddState(name);
            var transition = stateMachine.AddAnyStateTransition(state);
            transition.AddCondition(AnimatorConditionMode.If, 1.0f, name);
            transition.duration = 0.0f;
            transition.hasFixedDuration = true;
            state.AddStateMachineBehaviour<UIStyleAnimationState>();
            return Animator.StringToHash(name);
        }

        public override void Attach(Component component)
        {
            var selectable = component as Selectable;

            var animator = selectable.animator;
            if (null == selectable.animator)
                animator = selectable.gameObject.AddComponent<Animator>();

            selectable.transition = Selectable.Transition.None;
            selectable.transition = Selectable.Transition.Animation;

            if(_animatorController == null)
            {
                var controller = new AnimatorController();
                controller.name = "UISelectableStateController";
                controller.AddLayer("Base");

                var stateMachine = controller.layers[0].stateMachine;
                _triggerNormal = AddAnimationControllerState(controller, stateMachine, "Normal");
                _triggerHover = AddAnimationControllerState(controller, stateMachine, "Hover");
                _triggerPressed = AddAnimationControllerState(controller, stateMachine, "Pressed");
                _triggerSelected = AddAnimationControllerState(controller, stateMachine, "Selected");
                _triggerDisabled = AddAnimationControllerState(controller, stateMachine, "Disabled");
            }

            animator.runtimeAnimatorController = _animatorController;

            foreach (var behavior in animator.GetBehaviours<NoZ.UI.UIStyleAnimationState>())
                behavior.onStateEnter = OnAnimationState;
        }

        private void OnAnimationState(int hash)
        {
            _animationState = UIStyle.State.Normal;

            if (hash == _triggerHover)
                _animationState = UIStyle.State.Hover;
            else if (hash == _triggerPressed)
                _animationState = UIStyle.State.Pressed;
            else if (hash == _triggerDisabled)
                _animationState = UIStyle.State.Disabled;
            else if (hash == _triggerSelected)
                _animationState = UIStyle.State.Selected;

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
            if (_selected && _animationState == UIStyle.State.Hover)
                state = UIStyle.State.SelectedHover;
            else if (_selected && _animationState == UIStyle.State.Pressed)
                state = UIStyle.State.SelectedPressed;
            else
                state = _animationState;
        }
    }
}
